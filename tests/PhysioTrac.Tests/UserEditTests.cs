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

/// <summary>Administration › Users › Edit User (UserManagementService.UpdateAsync).</summary>
public class UserEditTests
{
    private sealed record Ctx(
        PhysioTracDbContext Db, UserManagementService Service, UserManager<ApplicationUser> Users, SessionService Sessions,
        Organization Org, TestCurrentUser Admin, ApplicationUser Staff);

    private static async Task<Ctx> SetupAsync()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var users = new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser, IdentityRole<Guid>, PhysioTracDbContext, Guid>(db),
            Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(), Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);
        var audit = new AuditService(db);
        var sessions = new SessionService(db, Options.Create(new SecurityOptions()));
        var service = new UserManagementService(
            db, users, new TenantAccessService(db, audit), sessions, audit, Options.Create(new AppOptions()));

        var org = new Organization { Name = "Org", Slug = "org" };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        await users.CreateAsync(new ApplicationUser
        {
            Id = admin.UserId,
            UserName = "admin",
            Email = "admin@test",
            FirstName = "Ada",
            LastName = "Admin",
            OrganizationId = org.Id,
            Role = UserRole.Admin,
        });
        var staff = new ApplicationUser
        {
            UserName = "afuentez",
            Email = "amanda@test",
            FirstName = "Amanda",
            LastName = "Fuentez",
            OrganizationId = org.Id,
            Role = UserRole.Therapist,
        };
        await users.CreateAsync(staff, "Original!Pass1");
        return new Ctx(db, service, users, sessions, org, admin, staff);
    }

    private static UpdateUserRequest Unchanged(ApplicationUser u, string? newPassword = null) =>
        new(u.UserName!, u.FirstName, u.LastName, u.Email, u.Role, u.Status, newPassword);

    [Fact]
    public async Task Update_ChangesUserIdNameEmailAndAccessLevel()
    {
        var c = await SetupAsync();

        var dto = await c.Service.UpdateAsync(c.Admin, c.Staff.Id,
            new UpdateUserRequest(" AFuentez2 ", "Amanda", "Fuentez-Ruiz", "Amanda.R@Test", UserRole.Director, UserStatus.Active, null));

        Assert.Equal("AFuentez2", dto.UserName);
        Assert.Equal("Fuentez-Ruiz", dto.LastName);
        Assert.Equal("amanda.r@test", dto.Email);
        Assert.Equal(UserRole.Director, dto.Role);
        Assert.NotNull(await c.Users.FindByNameAsync("afuentez2")); // normalized lookup still works
        Assert.True(await c.Db.AuditEvents.AnyAsync(a => a.Action == "user.updated" && a.ObjectId == c.Staff.Id));
    }

    [Fact]
    public async Task Update_NewPassword_ReplacesTheOldOne_AndSignsTheUserOut()
    {
        var c = await SetupAsync();
        var session = await c.Sessions.CreateSessionAsync(c.Staff.Id, c.Org.Id, UserRole.Therapist, null, null);

        await c.Service.UpdateAsync(c.Admin, c.Staff.Id, Unchanged(c.Staff, "Brand!New2026"));

        var reloaded = (await c.Users.FindByIdAsync(c.Staff.Id.ToString()))!;
        Assert.True(await c.Users.CheckPasswordAsync(reloaded, "Brand!New2026"));
        Assert.False(await c.Users.CheckPasswordAsync(reloaded, "Original!Pass1"));
        Assert.True(reloaded.MustChangePassword);
        Assert.Null(await c.Sessions.ValidateAndTouchAsync(session.SessionKey));
        var reset = await c.Db.AuditEvents.SingleAsync(a => a.Action == "user.password_reset");
        Assert.DoesNotContain("Brand!New2026", reset.MetadataJson);
    }

    [Fact]
    public async Task Update_BlankPassword_KeepsTheCurrentOne()
    {
        var c = await SetupAsync();

        await c.Service.UpdateAsync(c.Admin, c.Staff.Id, Unchanged(c.Staff, ""));

        var reloaded = (await c.Users.FindByIdAsync(c.Staff.Id.ToString()))!;
        Assert.True(await c.Users.CheckPasswordAsync(reloaded, "Original!Pass1"));
        Assert.False(await c.Db.AuditEvents.AnyAsync(a => a.Action == "user.password_reset"));
    }

    [Theory]
    [InlineData(UserStatus.Suspended)]
    [InlineData(UserStatus.Deleted)]
    public async Task Update_SuspendOrDelete_SignsTheUserOutEverywhere(UserStatus status)
    {
        var c = await SetupAsync();
        var session = await c.Sessions.CreateSessionAsync(c.Staff.Id, c.Org.Id, UserRole.Therapist, null, null);

        var dto = await c.Service.UpdateAsync(c.Admin, c.Staff.Id, Unchanged(c.Staff) with { Status = status });

        Assert.Equal(status, dto.Status);
        Assert.Null(await c.Sessions.ValidateAndTouchAsync(session.SessionKey));
    }

    [Fact]
    public async Task Update_BackToActive_ClearsSuspension()
    {
        var c = await SetupAsync();
        await c.Service.UpdateAsync(c.Admin, c.Staff.Id, Unchanged(c.Staff) with { Status = UserStatus.Suspended });

        var dto = await c.Service.UpdateAsync(c.Admin, c.Staff.Id, Unchanged(c.Staff) with { Status = UserStatus.Active });

        Assert.Equal(UserStatus.Active, dto.Status);
        var reloaded = (await c.Users.FindByIdAsync(c.Staff.Id.ToString()))!;
        Assert.Null(reloaded.SuspendedAt);
    }

    [Fact]
    public async Task Update_AdminCannotChangeOwnStatusOrAccessLevel_ButCanEditOwnName()
    {
        var c = await SetupAsync();
        var me = (await c.Users.FindByIdAsync(c.Admin.UserId.ToString()))!;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            c.Service.UpdateAsync(c.Admin, me.Id, Unchanged(me) with { Status = UserStatus.Suspended }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            c.Service.UpdateAsync(c.Admin, me.Id, Unchanged(me) with { Role = UserRole.Therapist }));

        var dto = await c.Service.UpdateAsync(c.Admin, me.Id, Unchanged(me) with { FirstName = "Adah" });
        Assert.Equal("Adah", dto.FirstName);
    }

    [Fact]
    public async Task Update_RejectsTakenUserIdOrEmail()
    {
        var c = await SetupAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            c.Service.UpdateAsync(c.Admin, c.Staff.Id, Unchanged(c.Staff) with { UserName = "ADMIN" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            c.Service.UpdateAsync(c.Admin, c.Staff.Id, Unchanged(c.Staff) with { Email = "admin@test" }));
    }

    [Fact]
    public async Task Update_RejectsBlankNamesAndSuperAdmin()
    {
        var c = await SetupAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            c.Service.UpdateAsync(c.Admin, c.Staff.Id, Unchanged(c.Staff) with { FirstName = "  " }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            c.Service.UpdateAsync(c.Admin, c.Staff.Id, Unchanged(c.Staff) with { Role = UserRole.SuperAdmin }));
    }

    [Fact]
    public async Task Update_PasswordForInvitedUser_ActivatesThemAndRetiresTheInviteLink()
    {
        var c = await SetupAsync();
        var (invited, _, _) = await c.Service.InviteAsync(c.Admin, new InviteUserRequest("New", "Hire", "new@test", UserRole.Scheduler));
        Assert.Equal(UserStatus.Inactive, invited.Status);
        var user = (await c.Users.FindByIdAsync(invited.Id.ToString()))!;

        var dto = await c.Service.UpdateAsync(c.Admin, user.Id, Unchanged(user, "Welcome!2026x"));

        Assert.Equal(UserStatus.Active, dto.Status);
        Assert.All(await c.Db.ClientInvitations.Where(i => i.UserId == user.Id).ToListAsync(), i => Assert.NotNull(i.UsedAt));
    }

    [Fact]
    public async Task Update_NonAdmin_IsForbidden_AndOtherOrgsUsersAreNotFound()
    {
        var c = await SetupAsync();
        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = c.Org.Id, Role = UserRole.Therapist };
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Service.UpdateAsync(therapist, c.Staff.Id, Unchanged(c.Staff)));

        var otherOrg = new Organization { Name = "Other", Slug = "other" };
        c.Db.Organizations.Add(otherOrg);
        await c.Db.SaveChangesAsync();
        var outsider = new ApplicationUser { UserName = "outsider", OrganizationId = otherOrg.Id, FirstName = "O", LastName = "S" };
        await c.Users.CreateAsync(outsider);
        await Assert.ThrowsAsync<NotFoundException>(() => c.Service.UpdateAsync(c.Admin, outsider.Id, Unchanged(outsider)));
    }
}
