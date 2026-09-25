using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Api.Controllers;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Allergies/medications/diagnoses all follow the identical
/// pattern -- resolve the patient via RequirePatientAccessAsync first, so
/// tenant/caseload scoping is exactly the chart's own, never looser. One
/// isolation test per controller proves that chokepoint is actually wired
/// up; TenantAccessServiceTests already covers RequirePatientAccessAsync's
/// own logic exhaustively, so this doesn't re-prove that underneath it.</summary>
public class PatientClinicalDetailControllersTests
{
    private static PhysioTracDbContext NewDb() => new(
        new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(PhysioTracDbContext Db, Organization Org1000, Patient PatientIn1000, Organization Org1001, Patient PatientIn1001)> SeedAsync()
    {
        var db = NewDb();
        var org1000 = new Organization { Name = "Org 1000", Slug = "org-1000" };
        var org1001 = new Organization { Name = "Org 1001", Slug = "org-1001" };
        var patientIn1000 = new Patient { OrganizationId = org1000.Id, FirstName = "Alpha", LastName = "Anderson", DateOfBirth = new DateOnly(1980, 1, 1) };
        var patientIn1001 = new Patient { OrganizationId = org1001.Id, FirstName = "Beta", LastName = "Brown", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Organizations.AddRange(org1000, org1001);
        db.Patients.AddRange(patientIn1000, patientIn1001);
        await db.SaveChangesAsync();
        return (db, org1000, patientIn1000, org1001, patientIn1001);
    }

    private static ControllerContext NewContext() => new() { HttpContext = new DefaultHttpContext() };

    // ---- Allergies ----

    [Fact]
    public async Task Allergies_Create_ThenList_RoundTrips()
    {
        // Admin, not Therapist -- Therapist is caseload-scoped and this
        // patient has no AssignedTherapistId set, which is the isolation
        // rule working as intended, not a bug in this test's setup.
        var (db, org1000, patient, _, _) = await SeedAsync();
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        var controller = new PatientAllergiesController(new TenantAccessService(db, new AuditService(db)), admin, db, new AuditService(db)) { ControllerContext = NewContext() };

        await controller.Create(patient.Id, new CreatePatientAllergyRequest("Penicillin", "Hives", AllergySeverity.Moderate, null));
        var result = Assert.IsType<OkObjectResult>(await controller.List(patient.Id));
        var allergies = Assert.IsAssignableFrom<IEnumerable<PatientAllergyDto>>(result.Value);
        Assert.Single(allergies);
    }

    [Fact]
    public async Task Allergies_ForAnotherOrganizationsPatient_Returns403()
    {
        var (db, org1000, _, _, patientIn1001) = await SeedAsync();
        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Therapist };
        var controller = new PatientAllergiesController(new TenantAccessService(db, new AuditService(db)), therapist, db, new AuditService(db)) { ControllerContext = NewContext() };

        var result = Assert.IsType<ObjectResult>(await controller.Create(
            patientIn1001.Id, new CreatePatientAllergyRequest("Latex", null, AllergySeverity.Mild, null)));
        Assert.Equal(403, result.StatusCode);
        Assert.Empty(await db.PatientAllergies.ToListAsync());
    }

    [Fact]
    public async Task Allergies_Deactivate_ExcludesFromDefaultList()
    {
        var (db, org1000, patient, _, _) = await SeedAsync();
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        var controller = new PatientAllergiesController(new TenantAccessService(db, new AuditService(db)), admin, db, new AuditService(db)) { ControllerContext = NewContext() };
        var created = Assert.IsType<CreatedAtActionResult>(await controller.Create(patient.Id, new CreatePatientAllergyRequest("Latex", null, AllergySeverity.Mild, null)));
        var dto = Assert.IsType<PatientAllergyDto>(created.Value);

        await controller.Deactivate(patient.Id, dto.Id, new DeactivateAllergyRequest("entered in error"));

        var result = Assert.IsType<OkObjectResult>(await controller.List(patient.Id));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<PatientAllergyDto>>(result.Value));
    }

    // ---- Medications ----

    [Fact]
    public async Task Medications_Create_ThenDiscontinue()
    {
        var (db, org1000, patient, _, _) = await SeedAsync();
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        var controller = new PatientMedicationsController(new TenantAccessService(db, new AuditService(db)), admin, db, new AuditService(db)) { ControllerContext = NewContext() };

        var created = Assert.IsType<CreatedAtActionResult>(await controller.Create(
            patient.Id, new CreatePatientMedicationRequest("Ibuprofen", "400mg", "As needed", null, null, null)));
        var dto = Assert.IsType<PatientMedicationDto>(created.Value);
        Assert.True(dto.IsActive);

        var discontinued = Assert.IsType<OkObjectResult>(await controller.Discontinue(patient.Id, dto.Id));
        Assert.False(Assert.IsType<PatientMedicationDto>(discontinued.Value).IsActive);
    }

    [Fact]
    public async Task Medications_ForAnotherOrganizationsPatient_Returns403()
    {
        var (db, org1000, _, _, patientIn1001) = await SeedAsync();
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        var controller = new PatientMedicationsController(new TenantAccessService(db, new AuditService(db)), admin, db, new AuditService(db)) { ControllerContext = NewContext() };

        var result = Assert.IsType<ObjectResult>(await controller.Create(
            patientIn1001.Id, new CreatePatientMedicationRequest("Ibuprofen", null, null, null, null, null)));
        Assert.Equal(403, result.StatusCode);
    }

    // ---- Diagnoses ----

    [Fact]
    public async Task Diagnoses_Create_ThenList_IncludesTheCodeDetails()
    {
        var (db, org1000, patient, _, _) = await SeedAsync();
        var code = new DiagnosisCode { Code = "M54.50", Description = "Low back pain, unspecified" };
        db.DiagnosisCodes.Add(code);
        await db.SaveChangesAsync();

        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        var controller = new PatientDiagnosesController(new TenantAccessService(db, new AuditService(db)), admin, db, new AuditService(db)) { ControllerContext = NewContext() };

        await controller.Create(patient.Id, new CreatePatientDiagnosisRequest(code.Id, true, DateOnly.FromDateTime(DateTime.Today), null));
        var result = Assert.IsType<OkObjectResult>(await controller.List(patient.Id));
        var diagnoses = Assert.IsAssignableFrom<IEnumerable<PatientDiagnosisDto>>(result.Value).ToList();

        var only = Assert.Single(diagnoses);
        Assert.Equal("M54.50", only.Code);
        Assert.True(only.IsPrimary);
    }

    [Fact]
    public async Task Diagnoses_Resolve_SetsResolvedDate()
    {
        var (db, org1000, patient, _, _) = await SeedAsync();
        var code = new DiagnosisCode { Code = "S93.401A", Description = "Ankle sprain" };
        db.DiagnosisCodes.Add(code);
        await db.SaveChangesAsync();

        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        var controller = new PatientDiagnosesController(new TenantAccessService(db, new AuditService(db)), admin, db, new AuditService(db)) { ControllerContext = NewContext() };
        var created = Assert.IsType<CreatedAtActionResult>(await controller.Create(patient.Id, new CreatePatientDiagnosisRequest(code.Id, true, null, null)));
        var dto = Assert.IsType<PatientDiagnosisDto>(created.Value);

        var resolved = Assert.IsType<OkObjectResult>(await controller.Resolve(patient.Id, dto.Id));
        Assert.True(Assert.IsType<PatientDiagnosisDto>(resolved.Value).IsResolved);
    }

    [Fact]
    public async Task Diagnoses_ForAnotherOrganizationsPatient_Returns403_AndCreatesNothing()
    {
        var (db, org1000, _, _, patientIn1001) = await SeedAsync();
        var code = new DiagnosisCode { Code = "M54.50", Description = "Low back pain, unspecified" };
        db.DiagnosisCodes.Add(code);
        await db.SaveChangesAsync();

        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        var controller = new PatientDiagnosesController(new TenantAccessService(db, new AuditService(db)), admin, db, new AuditService(db)) { ControllerContext = NewContext() };

        var result = Assert.IsType<ObjectResult>(await controller.Create(patientIn1001.Id, new CreatePatientDiagnosisRequest(code.Id, false, null, null)));
        Assert.Equal(403, result.StatusCode);
        Assert.Empty(await db.PatientDiagnoses.ToListAsync());
    }
}
