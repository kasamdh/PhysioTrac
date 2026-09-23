using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Api.Controllers;
using PhysioTrac.Application.Patients;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Drives PatientsController directly (no WebApplicationFactory/
/// Testcontainers -- Docker isn't available on this dev machine, so this
/// uses the same EF Core in-memory pattern as the rest of the suite) to
/// prove the actual HTTP-shaped behavior an Org 1000 caller gets when
/// touching Org 1001 data, and that a client can never smuggle an
/// OrganizationId into a create request.
///
/// One deliberate deviation from a literal "returns 404" expectation:
/// patient-scoped cross-org access goes through
/// RequirePatientAccessAsync/ForbiddenException, which this codebase maps
/// to 403 (see the session-wide Forbid() bug fix and the audited
/// "access.denied" event RequirePatientAccessAsync writes) -- an
/// intentional, tested, already-audited choice, not an oversight. A
/// different lookup style (e.g. ProviderLicensesController's
/// LoadProviderInOrgAsync) does use NotFoundException/404 for a resource
/// looked up by id within the wrong org. Both patterns coexist by design;
/// this suite documents the one Patient actually uses rather than forcing
/// a blanket 404 that doesn't match the shipped behavior.</summary>
public class CrossOrganizationIsolationTests
{
    private static PhysioTracDbContext NewDb() => new(
        new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static PatientsController NewController(PhysioTracDbContext db, TestCurrentUser user)
    {
        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var controller = new PatientsController(tenantAccess, user, db);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static async Task<(PhysioTracDbContext Db, Organization Org1000, Organization Org1001, Patient PatientIn1001)> SeedAsync()
    {
        var db = NewDb();
        var org1000 = new Organization { Name = "Org 1000", Slug = "org-1000", ClientNumber = 1000 };
        var org1001 = new Organization { Name = "Org 1001", Slug = "org-1001", ClientNumber = 1001 };
        var patientIn1001 = new Patient { OrganizationId = org1001.Id, FirstName = "Jordan", LastName = "Ellis", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Organizations.AddRange(org1000, org1001);
        db.Patients.Add(patientIn1001);
        await db.SaveChangesAsync();
        return (db, org1000, org1001, patientIn1001);
    }

    [Fact]
    public async Task Org1000Admin_Listing_NeverSeesOrg1001Patients()
    {
        var (db, org1000, _, patientIn1001) = await SeedAsync();
        var org1000Admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        var controller = NewController(db, org1000Admin);

        var result = Assert.IsType<OkObjectResult>(await controller.List());
        var patients = Assert.IsAssignableFrom<IEnumerable<PatientDto>>(result.Value);
        Assert.DoesNotContain(patients, p => p.Id == patientIn1001.Id);
    }

    [Fact]
    public async Task Org1000Admin_Reading_Org1001Patient_Returns403()
    {
        var (db, org1000, _, patientIn1001) = await SeedAsync();
        var org1000Admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        var controller = NewController(db, org1000Admin);

        var result = Assert.IsType<ObjectResult>(await controller.Get(patientIn1001.Id));
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task Org1000Admin_Updating_Org1001Patient_Returns403_AndNeverWritesTheChange()
    {
        var (db, org1000, _, patientIn1001) = await SeedAsync();
        var org1000Admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        var controller = NewController(db, org1000Admin);

        var result = Assert.IsType<ObjectResult>(await controller.Update(
            patientIn1001.Id, new UpdatePatientRequest("Hacked", "Name", null, null, null, PatientStatus.Active)));
        Assert.Equal(403, result.StatusCode);

        var unchanged = await db.Patients.FindAsync(patientIn1001.Id);
        Assert.Equal("Jordan", unchanged!.FirstName);
    }

    [Fact]
    public void CreatePatientRequest_HasNoOrganizationIdField_SoOneCanNeverBeSmuggledIn()
    {
        // The strongest possible guarantee: there's no property for a client
        // to even populate, regardless of how lenient JSON binding is.
        Assert.Null(typeof(CreatePatientRequest).GetProperty("OrganizationId"));
    }

    [Fact]
    public async Task CreatingAPatient_AlwaysUsesTheCallersOrganization_RegardlessOfAnythingElseInTheRequest()
    {
        var (db, org1000, org1001, _) = await SeedAsync();
        var org1000Scheduler = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Scheduler };
        var controller = NewController(db, org1000Scheduler);

        var created = Assert.IsType<CreatedAtActionResult>(await controller.Create(
            new CreatePatientRequest("New", "Patient", new DateOnly(1995, 1, 1), null, null, null)));
        var dto = Assert.IsType<PatientDto>(created.Value);

        var persisted = await db.Patients.FindAsync(dto.Id);
        Assert.Equal(org1000.Id, persisted!.OrganizationId);
        Assert.NotEqual(org1001.Id, persisted.OrganizationId);
    }

    [Fact]
    public async Task UnauthorizedRole_CreatingAPatient_Returns403()
    {
        // Biller is not in RoleSets.Scheduling -- PatientsController.Create
        // requires it.
        var (db, org1000, _, _) = await SeedAsync();
        var biller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Biller };
        var controller = NewController(db, biller);

        var result = Assert.IsType<ObjectResult>(await controller.Create(
            new CreatePatientRequest("Should", "Fail", new DateOnly(1995, 1, 1), null, null, null)));
        Assert.Equal(403, result.StatusCode);
    }
}
