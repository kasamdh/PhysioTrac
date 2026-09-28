using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>AccessControl is process-wide, so tests that flip it must never
/// overlap with anything else: this collection runs on its own, after the
/// parallel ones.</summary>
[CollectionDefinition(nameof(AccessControlSwitchCollection), DisableParallelization = true)]
public class AccessControlSwitchCollection;

/// <summary>With role-based access control switched OFF (development only),
/// every staff role passes every role check -- but authentication-level data
/// boundaries still hold: organization isolation, and a Patient-role portal
/// account seeing only its own chart.</summary>
[Collection(nameof(AccessControlSwitchCollection))]
public sealed class AccessControlSwitchTests : IDisposable
{
    public AccessControlSwitchTests() => AccessControl.Enabled = false;

    public void Dispose() => AccessControl.Enabled = true;

    private static TestCurrentUser User(SchedulingFixture f, UserRole role) =>
        new() { UserId = Guid.NewGuid(), OrganizationId = f.Org.Id, Role = role };

    [Theory]
    [InlineData(UserRole.Biller)]
    [InlineData(UserRole.Compliance)]
    [InlineData(UserRole.Assistant)]
    [InlineData(UserRole.Scheduler)]
    public void EveryStaffRole_PassesEveryRoleCheck_IncludingAdHocRoleLists(UserRole role)
    {
        var f = new SchedulingFixture();
        var tenantAccess = new TenantAccessService(f.Db, new AuditService(f.Db));
        var user = User(f, role);

        tenantAccess.RequireRole(user, RoleSets.OrganizationAdministration);
        tenantAccess.RequireRole(user, RoleSets.Billing);
        tenantAccess.RequireRole(user, new HashSet<UserRole> { UserRole.Director });
        tenantAccess.RequirePlatformSuperAdmin(user);
        Assert.True(RoleSets.ScheduleOverride.Contains(role));
    }

    [Fact]
    public void PatientPortalAccounts_AreNeverWaved_Through()
    {
        var f = new SchedulingFixture();
        var tenantAccess = new TenantAccessService(f.Db, new AuditService(f.Db));
        var portal = User(f, UserRole.Patient);

        Assert.Throws<ForbiddenException>(() => tenantAccess.RequireRole(portal, RoleSets.AllStaff));
        Assert.Throws<ForbiddenException>(() => tenantAccess.RequirePlatformSuperAdmin(portal));
        Assert.False(RoleSets.Clinical.Contains(UserRole.Patient));
    }

    [Fact]
    public async Task ABillerCanRegisterAndBookAPatient_AndAPtaSeesTheWholeRoster()
    {
        var f = new SchedulingFixture();

        // Biller normally can't book (not a Scheduling role).
        var booked = await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), User(f, UserRole.Biller));
        Assert.Equal(AppointmentStatus.Scheduled, booked.Status);

        // An Assistant with nothing assigned normally sees no patients.
        var tenantAccess = new TenantAccessService(f.Db, new AuditService(f.Db));
        Assert.Equal(2, tenantAccess.PatientsFor(User(f, UserRole.Assistant)).Count());
    }

    [Fact]
    public async Task OrganizationIsolation_StillHolds()
    {
        var f = new SchedulingFixture();
        await f.Appointments.CreateAsync(f.Book(f.Pt, f.At(9)), f.Scheduler);
        var otherOrgBiller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = f.OtherOrg.Id, Role = UserRole.Biller };
        var tenantAccess = new TenantAccessService(f.Db, new AuditService(f.Db));

        Assert.Empty(tenantAccess.PatientsFor(otherOrgBiller));
        Assert.Empty((await f.Schedule.GetRangeAsync(otherOrgBiller, new Application.Scheduling.ScheduleQuery(f.At(0), f.At(23)))).Appointments);
        await Assert.ThrowsAsync<ForbiddenException>(() => tenantAccess.RequirePatientAccessAsync(otherOrgBiller, f.Patient.Id));
    }

    [Fact]
    public async Task ThePlatformSuperAdmin_WorksInsideTheDefaultClinic_ForEveryClinicModule()
    {
        var f = new SchedulingFixture();
        AccessControl.SuperAdminOrganizationId = f.Org.Id;
        try
        {
            // The real claims-based user: a SuperAdmin has no organization claim.
            var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [
                new(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new(Infrastructure.Identity.AppClaimTypes.Role, nameof(UserRole.SuperAdmin)),
                new(Infrastructure.Identity.AppClaimTypes.IsPlatformSuperAdmin, "true"),
            ], "test"));
            var superAdmin = new Infrastructure.Identity.ClaimsCurrentUser(
                new Microsoft.AspNetCore.Http.HttpContextAccessor(),
                new Infrastructure.Identity.CurrentUserAccessor { Principal = principal });
            var tenantAccess = new TenantAccessService(f.Db, new AuditService(f.Db));

            Assert.Equal(f.Org.Id, superAdmin.OrganizationId);
            Assert.Equal(f.Org.Id, (await tenantAccess.OrganizationRequiredAsync(superAdmin)).Id);
            Assert.Equal(2, tenantAccess.PatientsFor(superAdmin).Count());
            Assert.Equal(2, (await f.Schedule.GetDayAsync(superAdmin, f.Monday, null, null)).Providers.Count);
            tenantAccess.RequirePlatformSuperAdmin(superAdmin); // still a super admin too

            // With access control back on, the SuperAdmin is kept out of clinic data again.
            AccessControl.Enabled = true;
            Assert.Null(superAdmin.OrganizationId);
            await Assert.ThrowsAsync<ForbiddenException>(() => tenantAccess.OrganizationRequiredAsync(superAdmin));
        }
        finally
        {
            AccessControl.SuperAdminOrganizationId = null;
        }
    }

    [Fact]
    public void SwitchingItBackOn_RestoresNormalChecks()
    {
        var f = new SchedulingFixture();
        var tenantAccess = new TenantAccessService(f.Db, new AuditService(f.Db));
        AccessControl.Enabled = true;

        Assert.Throws<ForbiddenException>(() => tenantAccess.RequireRole(User(f, UserRole.Biller), RoleSets.OrganizationAdministration));
        Assert.False(RoleSets.OrganizationAdministration.Contains(UserRole.Biller));
    }
}
