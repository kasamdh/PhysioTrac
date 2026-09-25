using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Configuration;
using PhysioTrac.Application.Users;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Org-scoped staff administration: invite/role-change/activate/
/// deactivate. Every mutating method requires OrganizationAdministration
/// (Admin/Director) and resolves the org strictly from the actor's own
/// claims -- these tests exercise both the role gate and cross-tenant
/// isolation the same way TenantAccessServiceTests does for patients.</summary>
public class UserManagementServiceTests
{
    private static (PhysioTracDbContext Db, UserManagementService Service, UserManager<ApplicationUser> Users) NewService()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var userStore = new UserStore<ApplicationUser, IdentityRole<Guid>, PhysioTracDbContext, Guid>(db);
        var userManager = new UserManager<ApplicationUser>(
            userStore, Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(), Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var sessions = new SessionService(db, Options.Create(new SecurityOptions()));
        var service = new UserManagementService(db, userManager, tenantAccess, sessions, audit, Options.Create(new AppOptions()));
        return (db, service, userManager);
    }

    private static async Task<(Organization Org1000, Organization Org1001)> SeedOrgsAsync(PhysioTracDbContext db)
    {
        var org1000 = new Organization { Name = "Org 1000", Slug = "org-1000" };
        var org1001 = new Organization { Name = "Org 1001", Slug = "org-1001" };
        db.Organizations.AddRange(org1000, org1001);
        await db.SaveChangesAsync();
        return (org1000, org1001);
    }

    private static TestCurrentUser Admin(Guid orgId) => new() { UserId = Guid.NewGuid(), OrganizationId = orgId, Role = UserRole.Admin };

    [Fact]
    public async Task Invite_CreatesUserWithNoUsablePassword_AndAnActivationInvitation_AndAuditsIt()
    {
        var (db, service, users) = NewService();
        var (org1000, _) = await SeedOrgsAsync(db);
        var admin = Admin(org1000.Id);

        var (dto, token, activationUrl) = await service.InviteAsync(
            admin, new InviteUserRequest("Morgan", "Patel", "scheduler@sourcemotionpt.test", UserRole.Scheduler));

        Assert.False(string.IsNullOrEmpty(token));
        Assert.Contains("org-1000/activate?token=", activationUrl);

        var created = await users.FindByIdAsync(dto.Id.ToString());
        Assert.NotNull(created);
        Assert.False(await users.HasPasswordAsync(created!));
        Assert.Equal(UserRole.Scheduler, created!.Role);
        Assert.Equal(org1000.Id, created.OrganizationId);

        var invitation = await db.ClientInvitations.SingleAsync(i => i.UserId == created.Id);
        Assert.True(invitation.IsUsable);

        var auditEvents = await db.AuditEvents.Where(e => e.ObjectId == created.Id && e.Action == "user.invited").ToListAsync();
        Assert.Single(auditEvents);
    }

    [Fact]
    public async Task Invite_ByNonAdministrationRole_ThrowsForbidden_AndCreatesNoUser()
    {
        var (db, service, _) = NewService();
        var (org1000, _) = await SeedOrgsAsync(db);
        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Therapist };

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.InviteAsync(therapist, new InviteUserRequest("New", "Hire", "new.hire@sourcemotionpt.test", UserRole.Scheduler)));

        Assert.Empty(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task Invite_WithSuperAdminRole_IsRejected()
    {
        var (db, service, _) = NewService();
        var (org1000, _) = await SeedOrgsAsync(db);
        var admin = Admin(org1000.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.InviteAsync(admin, new InviteUserRequest("Would", "BeSuperAdmin", "nope@sourcemotionpt.test", UserRole.SuperAdmin)));
    }

    [Fact]
    public async Task List_OnlyReturnsCallersOwnOrganizationsUsers()
    {
        var (db, service, users) = NewService();
        var (org1000, org1001) = await SeedOrgsAsync(db);
        await users.CreateAsync(new ApplicationUser { UserName = "s1000", Email = "s1000@test", OrganizationId = org1000.Id, Role = UserRole.Scheduler });
        await users.CreateAsync(new ApplicationUser { UserName = "s1001", Email = "s1001@test", OrganizationId = org1001.Id, Role = UserRole.Scheduler });

        var admin = Admin(org1000.Id);
        var list = await service.ListAsync(admin);

        var user = Assert.Single(list);
        Assert.Equal("s1000", user.UserName);
    }

    [Fact]
    public async Task ChangeRole_OnAnotherOrganizationsUser_ThrowsNotFound_AndChangesNothing()
    {
        var (db, service, users) = NewService();
        var (org1000, org1001) = await SeedOrgsAsync(db);
        var otherOrgUser = new ApplicationUser { UserName = "s1001", Email = "s1001@test", OrganizationId = org1001.Id, Role = UserRole.Scheduler };
        await users.CreateAsync(otherOrgUser);

        var admin = Admin(org1000.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => service.ChangeRoleAsync(admin, otherOrgUser.Id, UserRole.Admin));

        var unchanged = await users.FindByIdAsync(otherOrgUser.Id.ToString());
        Assert.Equal(UserRole.Scheduler, unchanged!.Role);
    }

    [Fact]
    public async Task ChangeRole_WritesFromAndToInAuditMetadata()
    {
        var (db, service, users) = NewService();
        var (org1000, _) = await SeedOrgsAsync(db);
        var user = new ApplicationUser { UserName = "s1000", Email = "s1000@test", OrganizationId = org1000.Id, Role = UserRole.Scheduler };
        await users.CreateAsync(user);

        var admin = Admin(org1000.Id);
        var updated = await service.ChangeRoleAsync(admin, user.Id, UserRole.Biller);

        Assert.Equal(UserRole.Biller, updated.Role);
        var auditEvent = await db.AuditEvents.SingleAsync(e => e.ObjectId == user.Id && e.Action == "user.role_changed");
        Assert.Contains("Scheduler", auditEvent.MetadataJson);
        Assert.Contains("Biller", auditEvent.MetadataJson);
    }

    [Fact]
    public async Task Deactivate_RevokesActiveSessions_SoTheNextRequestIsRejected()
    {
        var (db, service, users) = NewService();
        var (org1000, _) = await SeedOrgsAsync(db);
        var user = new ApplicationUser { UserName = "s1000", Email = "s1000@test", OrganizationId = org1000.Id, Role = UserRole.Scheduler };
        await users.CreateAsync(user);

        var sessions = new SessionService(db, Options.Create(new SecurityOptions()));
        var session = await sessions.CreateSessionAsync(user.Id, org1000.Id, UserRole.Scheduler, null, null);

        var admin = Admin(org1000.Id);
        var deactivated = await service.DeactivateAsync(admin, user.Id, "No longer employed");

        Assert.Equal(UserStatus.Suspended, deactivated.Status);
        var revalidated = await sessions.ValidateAndTouchAsync(session.SessionKey);
        Assert.Null(revalidated);
    }

    [Fact]
    public async Task Deactivate_CannotTargetYourOwnAccount()
    {
        var (db, service, users) = NewService();
        var (org1000, _) = await SeedOrgsAsync(db);
        var admin = Admin(org1000.Id);
        var adminUser = new ApplicationUser { Id = admin.UserId, UserName = "admin", Email = "admin@test", OrganizationId = org1000.Id, Role = UserRole.Admin };
        await users.CreateAsync(adminUser);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeactivateAsync(admin, admin.UserId, null));
    }

    [Fact]
    public async Task Activate_ClearsSuspensionState()
    {
        var (db, service, users) = NewService();
        var (org1000, _) = await SeedOrgsAsync(db);
        var user = new ApplicationUser
        {
            UserName = "s1000",
            Email = "s1000@test",
            OrganizationId = org1000.Id,
            Role = UserRole.Scheduler,
            Status = UserStatus.Suspended,
            SuspendedAt = DateTimeOffset.UtcNow,
            SuspensionReason = "test",
        };
        await users.CreateAsync(user);

        var admin = Admin(org1000.Id);
        var reactivated = await service.ActivateAsync(admin, user.Id);

        Assert.Equal(UserStatus.Active, reactivated.Status);
        var persisted = await users.FindByIdAsync(user.Id.ToString());
        Assert.Null(persisted!.SuspendedAt);
        Assert.Null(persisted.SuspensionReason);
    }
}
