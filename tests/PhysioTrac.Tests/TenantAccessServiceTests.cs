using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;
using Xunit;

namespace PhysioTrac.Tests;

/// <summary>Direct port of the tenant-isolation intent behind the original
/// suite's `test_cross_organization_*` / `test_tenant_isolation_*` tests —
/// "a Client 1000 user must never access Client 1001 data" is this system's
/// stated non-negotiable, so this class exercises it against
/// <see cref="TenantAccessService"/> the same way the Django tests exercised
/// `care/access.py`.</summary>
public class TenantAccessServiceTests
{
    private static PhysioTracDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<PhysioTracDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PhysioTracDbContext(options);
    }

    private static (PhysioTracDbContext Db, Organization OrgA, Organization OrgB, Patient PatientA, Patient PatientB, Guid TherapistAId)
        SeedTwoTenants()
    {
        var db = NewDb();
        var orgA = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var orgB = new Organization { Name = "Client B", Slug = "client-b", ClientNumber = 1001 };
        var therapistAId = Guid.NewGuid();

        var patientA = new Patient
        {
            OrganizationId = orgA.Id, FirstName = "Alice", LastName = "Anderson",
            DateOfBirth = new DateOnly(1990, 1, 1), AssignedTherapistId = therapistAId,
        };
        var patientB = new Patient
        {
            OrganizationId = orgB.Id, FirstName = "Bob", LastName = "Brown",
            DateOfBirth = new DateOnly(1985, 5, 5),
        };

        db.Organizations.AddRange(orgA, orgB);
        db.Patients.AddRange(patientA, patientB);
        db.SaveChanges();

        return (db, orgA, orgB, patientA, patientB, therapistAId);
    }

    [Fact]
    public async Task RequirePatientAccess_CrossOrganizationPatient_IsRejected()
    {
        var (db, orgA, _, _, patientB, _) = SeedTwoTenants();
        var audit = new AuditService(db);
        var service = new TenantAccessService(db, audit);

        var adminInOrgA = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = orgA.Id, Role = UserRole.Admin };

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.RequirePatientAccessAsync(adminInOrgA, patientB.Id));
    }

    [Fact]
    public async Task RequirePatientAccess_CrossOrganizationDenial_IsAudited()
    {
        var (db, orgA, _, _, patientB, _) = SeedTwoTenants();
        var audit = new AuditService(db);
        var service = new TenantAccessService(db, audit);
        var adminInOrgA = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = orgA.Id, Role = UserRole.Admin };

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.RequirePatientAccessAsync(adminInOrgA, patientB.Id));

        var events = await db.AuditEvents.ToListAsync();
        Assert.Contains(events, e => e.Action == "access.denied" && e.PatientId == patientB.Id);
    }

    [Fact]
    public async Task PatientsFor_Therapist_OnlySeesAssignedCaseload()
    {
        var (db, orgA, _, patientA, _, therapistAId) = SeedTwoTenants();
        // A second, unassigned patient in the same org the therapist should NOT see.
        var unassigned = new Patient { OrganizationId = orgA.Id, FirstName = "Carl", LastName = "Clark", DateOfBirth = new DateOnly(1970, 1, 1) };
        db.Patients.Add(unassigned);
        await db.SaveChangesAsync();

        var audit = new AuditService(db);
        var service = new TenantAccessService(db, audit);
        var therapist = new TestCurrentUser { UserId = therapistAId, OrganizationId = orgA.Id, Role = UserRole.Therapist };

        var visible = service.PatientsFor(therapist).ToList();

        Assert.Single(visible);
        Assert.Equal(patientA.Id, visible[0].Id);
    }

    [Fact]
    public void PatientsFor_Admin_SeesWholeOrganization()
    {
        var (db, orgA, _, patientA, _, _) = SeedTwoTenants();
        var audit = new AuditService(db);
        var service = new TenantAccessService(db, audit);
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = orgA.Id, Role = UserRole.Admin };

        var visible = service.PatientsFor(admin).ToList();

        Assert.Single(visible);
        Assert.Equal(patientA.Id, visible[0].Id);
    }

    [Fact]
    public void PatientsFor_PatientRole_IsScopedToOwnChartRegardlessOfClinicalFlag()
    {
        var db = NewDb();
        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var portalUserId = Guid.NewGuid();
        var ownChart = new Patient { OrganizationId = org.Id, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1), PortalUserId = portalUserId };
        var otherChart = new Patient { OrganizationId = org.Id, FirstName = "Other", LastName = "Person", DateOfBirth = new DateOnly(1991, 1, 1) };
        db.Organizations.Add(org);
        db.Patients.AddRange(ownChart, otherChart);
        db.SaveChanges();

        var audit = new AuditService(db);
        var service = new TenantAccessService(db, audit);
        var patientUser = new TestCurrentUser { UserId = portalUserId, OrganizationId = org.Id, Role = UserRole.Patient };

        // Even with clinical=false (the "whole org" toggle for every other role),
        // a patient-role caller must only ever see their own chart.
        var visible = service.PatientsFor(patientUser, clinical: false).ToList();

        Assert.Single(visible);
        Assert.Equal(ownChart.Id, visible[0].Id);
    }

    [Fact]
    public async Task OrganizationRequired_SuspendedOrganization_IsRejected()
    {
        var db = NewDb();
        var org = new Organization { Name = "Client A", Slug = "client-a", Status = OrganizationStatus.Suspended };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var audit = new AuditService(db);
        var service = new TenantAccessService(db, audit);
        var user = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };

        await Assert.ThrowsAsync<ForbiddenException>(() => service.OrganizationRequiredAsync(user));
    }

    [Fact]
    public async Task OrganizationRequired_ArchivedOrganization_IsRejected()
    {
        var db = NewDb();
        var org = new Organization { Name = "Client A", Slug = "client-a", ArchivedAt = DateTimeOffset.UtcNow };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var audit = new AuditService(db);
        var service = new TenantAccessService(db, audit);
        var user = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };

        await Assert.ThrowsAsync<ForbiddenException>(() => service.OrganizationRequiredAsync(user));
    }

    [Fact]
    public async Task OrganizationRequired_PlatformSuperAdmin_HasNoStandingAccess()
    {
        var db = NewDb();
        var audit = new AuditService(db);
        var service = new TenantAccessService(db, audit);
        var superAdmin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = null, Role = UserRole.SuperAdmin, IsPlatformSuperAdmin = true };

        await Assert.ThrowsAsync<ForbiddenException>(() => service.OrganizationRequiredAsync(superAdmin));
    }
}
