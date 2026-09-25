using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;
using Xunit;

namespace PhysioTrac.Tests;

/// <summary>The reauthentication step this phase adds on top of the
/// already-existing break-glass workflow (reason, time-boxed duration,
/// revocation, audit trail) -- RequestAsync must re-verify the actor's own
/// current password before issuing a grant, even though they're already
/// authenticated.</summary>
public class PrivilegedAccessServiceTests
{
    private const string Password = "SuperSecret123!";

    private static async Task<(PhysioTracDbContext Db, PrivilegedAccessService Service, Organization Org, ApplicationUser SuperAdminUser)> NewServiceAsync()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var userStore = new UserStore<ApplicationUser, IdentityRole<Guid>, PhysioTracDbContext, Guid>(db);
        var userManager = new UserManager<ApplicationUser>(
            userStore, Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(), Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);

        var superAdminUser = new ApplicationUser { UserName = "superadmin", Email = "superadmin@physiotrac.test", Role = UserRole.SuperAdmin };
        await userManager.CreateAsync(superAdminUser, Password);

        var audit = new AuditService(db);
        var service = new PrivilegedAccessService(db, audit, userManager);
        return (db, service, org, superAdminUser);
    }

    private static TestCurrentUser SuperAdmin(Guid userId) => new()
    {
        UserId = userId, OrganizationId = null, Role = UserRole.SuperAdmin, IsPlatformSuperAdmin = true,
    };

    [Fact]
    public async Task RequestAsync_CorrectCurrentPassword_IssuesAGrant()
    {
        var (db, service, org, superAdminUser) = await NewServiceAsync();
        var actor = SuperAdmin(superAdminUser.Id);

        var grant = await service.RequestAsync(org.Id, actor, "Investigating a support ticket", 4, Password);

        Assert.True(grant.IsActive);
        Assert.Equal("Investigating a support ticket", grant.Reason);
    }

    [Fact]
    public async Task RequestAsync_WrongCurrentPassword_Throws_AndPersistsNoGrant()
    {
        var (db, service, org, superAdminUser) = await NewServiceAsync();
        var actor = SuperAdmin(superAdminUser.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.RequestAsync(org.Id, actor, "Investigating a support ticket", 4, "definitely-not-the-password"));

        Assert.Empty(await db.PrivilegedAccessGrants.ToListAsync());
    }

    [Fact]
    public async Task RequestAsync_UnsupportedDuration_Throws()
    {
        var (_, service, org, superAdminUser) = await NewServiceAsync();
        var actor = SuperAdmin(superAdminUser.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RequestAsync(org.Id, actor, "reason", 999, Password));
    }

    [Fact]
    public async Task RequestAsync_BlankReason_Throws()
    {
        var (_, service, org, superAdminUser) = await NewServiceAsync();
        var actor = SuperAdmin(superAdminUser.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RequestAsync(org.Id, actor, "   ", 4, Password));
    }

    [Fact]
    public async Task RevokeAsync_ActiveGrant_DeactivatesIt()
    {
        var (_, service, org, superAdminUser) = await NewServiceAsync();
        var actor = SuperAdmin(superAdminUser.Id);
        var grant = await service.RequestAsync(org.Id, actor, "reason", 1, Password);

        var revoked = await service.RevokeAsync(grant.Id, actor);

        Assert.False(revoked.IsActive);
        Assert.NotNull(revoked.RevokedAt);
    }

    [Fact]
    public async Task ActiveGrantAsync_ReturnsNull_AfterExpiry()
    {
        var (db, service, org, superAdminUser) = await NewServiceAsync();
        var actor = SuperAdmin(superAdminUser.Id);
        var grant = await service.RequestAsync(org.Id, actor, "reason", 1, Password);
        grant.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var active = await service.ActiveGrantAsync(org.Id, actor.UserId);

        Assert.Null(active);
    }
}
