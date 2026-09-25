using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Api.Controllers;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Phase 9's audit-completeness ask, made concrete: every entity
/// that mutates but carries no direct OrganizationId of its own (so
/// EntityChangeAuditInterceptor never sees it) must get an explicit
/// RecordAuditEventAsync call somewhere in its write path, or its history
/// is unrecoverable. This file is a regression lock on the gaps a Phase 9
/// audit found and Phase 9 itself closed: PatientAllergy, PatientMedication,
/// PatientDiagnosis, Room, Superbill, PaymentRecord, PatientPayment, and
/// OutcomeScore. Every other mutating entity in this app either carries a
/// direct OrganizationId (auto-audited) or already had an explicit audit
/// call from the phase that introduced it -- this file only re-proves the
/// ones that didn't.</summary>
public class AuditLogCompletenessTests
{
    private static PhysioTracDbContext NewDb() => new(
        new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(PhysioTracDbContext Db, Organization Org, Patient Patient, TestCurrentUser Admin)> SeedAsync()
    {
        var db = NewDb();
        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var patient = new Patient { OrganizationId = org.Id, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Organizations.Add(org);
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        return (db, org, patient, admin);
    }

    private static ControllerContext NewContext() => new() { HttpContext = new DefaultHttpContext() };

    [Fact]
    public async Task PatientAllergy_CreateAndDeactivate_BothAudited()
    {
        var (db, org, patient, admin) = await SeedAsync();
        var audit = new AuditService(db);
        var controller = new PatientAllergiesController(new TenantAccessService(db, audit), admin, db, audit) { ControllerContext = NewContext() };

        var created = Assert.IsType<CreatedAtActionResult>(
            await controller.Create(patient.Id, new CreatePatientAllergyRequest("Penicillin", "Hives", AllergySeverity.Moderate, null)));
        var dto = Assert.IsType<PatientAllergyDto>(created.Value);
        await controller.Deactivate(patient.Id, dto.Id, null);

        var actions = await db.AuditEvents.Where(e => e.ObjectId == dto.Id).Select(e => e.Action).ToListAsync();
        Assert.Contains("patient_allergy.created", actions);
        Assert.Contains("patient_allergy.deactivated", actions);
    }

    [Fact]
    public async Task PatientMedication_CreateAndDiscontinue_BothAudited()
    {
        var (db, org, patient, admin) = await SeedAsync();
        var audit = new AuditService(db);
        var controller = new PatientMedicationsController(new TenantAccessService(db, audit), admin, db, audit) { ControllerContext = NewContext() };

        var created = Assert.IsType<CreatedAtActionResult>(
            await controller.Create(patient.Id, new CreatePatientMedicationRequest("Ibuprofen", "400mg", "As needed", null, null, null)));
        var dto = Assert.IsType<PatientMedicationDto>(created.Value);
        await controller.Discontinue(patient.Id, dto.Id);

        var actions = await db.AuditEvents.Where(e => e.ObjectId == dto.Id).Select(e => e.Action).ToListAsync();
        Assert.Contains("patient_medication.created", actions);
        Assert.Contains("patient_medication.discontinued", actions);
    }

    [Fact]
    public async Task PatientDiagnosis_CreateAndResolve_BothAudited()
    {
        var (db, org, patient, admin) = await SeedAsync();
        var code = new DiagnosisCode { Code = "M54.50", Description = "Low back pain, unspecified" };
        db.DiagnosisCodes.Add(code);
        await db.SaveChangesAsync();
        var audit = new AuditService(db);
        var controller = new PatientDiagnosesController(new TenantAccessService(db, audit), admin, db, audit) { ControllerContext = NewContext() };

        var created = Assert.IsType<CreatedAtActionResult>(
            await controller.Create(patient.Id, new CreatePatientDiagnosisRequest(code.Id, true, null, null)));
        var dto = Assert.IsType<PatientDiagnosisDto>(created.Value);
        await controller.Resolve(patient.Id, dto.Id);

        var actions = await db.AuditEvents.Where(e => e.ObjectId == dto.Id).Select(e => e.Action).ToListAsync();
        Assert.Contains("patient_diagnosis.created", actions);
        Assert.Contains("patient_diagnosis.resolved", actions);
    }

    [Fact]
    public async Task Room_CreateAndDeactivate_BothAudited()
    {
        var db = NewDb();
        var org = new Organization { Name = "Client A", Slug = "client-a" };
        var location = new Location { OrganizationId = org.Id, Name = "Main Clinic" };
        db.Organizations.Add(org);
        db.Locations.Add(location);
        await db.SaveChangesAsync();
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        var audit = new AuditService(db);
        var controller = new RoomsController(new TenantAccessService(db, audit), admin, db, audit) { ControllerContext = NewContext() };

        var created = Assert.IsType<CreatedAtActionResult>(await controller.Create(location.Id, new CreateRoomRequest("Treatment Room 1")));
        var dto = Assert.IsType<RoomDto>(created.Value);
        await controller.Deactivate(location.Id, dto.Id);

        var actions = await db.AuditEvents.Where(e => e.ObjectId == dto.Id).Select(e => e.Action).ToListAsync();
        Assert.Contains("room.created", actions);
        Assert.Contains("room.deactivated", actions);
    }

    [Fact]
    public async Task Superbill_Create_IsAudited()
    {
        var (db, org, patient, _) = await SeedAsync();
        var biller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Biller };
        var audit = new AuditService(db);
        var controller = new SuperbillsController(new TenantAccessService(db, audit), biller, db, audit) { ControllerContext = NewContext() };

        var created = Assert.IsType<CreatedAtActionResult>(await controller.Create(
            new CreateSuperbillRequest(patient.Id, Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), null, 100m, null)));
        var superbill = Assert.IsType<Superbill>(created.Value);

        var actions = await db.AuditEvents.Where(e => e.ObjectId == superbill.Id).Select(e => e.Action).ToListAsync();
        Assert.Contains("superbill.created", actions);
    }

    [Fact]
    public async Task PaymentRecord_Create_IsAudited()
    {
        var (db, org, patient, _) = await SeedAsync();
        var biller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Biller };
        var audit = new AuditService(db);
        var controller = new PaymentRecordsController(new TenantAccessService(db, audit), biller, db, audit) { ControllerContext = NewContext() };

        var created = Assert.IsType<CreatedAtActionResult>(await controller.Create(
            new CreatePaymentRecordRequest(patient.Id, null, 50m, null, ClaimTransactionMethod.Check, "ref-123")));
        var payment = Assert.IsType<PaymentRecord>(created.Value);

        var actions = await db.AuditEvents.Where(e => e.ObjectId == payment.Id).Select(e => e.Action).ToListAsync();
        Assert.Contains("payment_record.created", actions);
    }

    [Fact]
    public async Task PatientPayment_PortalCreate_IsAudited()
    {
        var (db, org, patient, _) = await SeedAsync();
        var portalUserId = Guid.NewGuid();
        patient.PortalUserId = portalUserId;
        await db.SaveChangesAsync();
        var portalUser = new TestCurrentUser { UserId = portalUserId, OrganizationId = org.Id, Role = UserRole.Patient };
        var audit = new AuditService(db);
        var controller = new PatientPaymentsController(new TenantAccessService(db, audit), portalUser, db, audit) { ControllerContext = NewContext() };

        var created = Assert.IsType<CreatedAtActionResult>(await controller.Create(
            new CreatePatientPaymentRequest(75m, PatientPaymentStatus.Succeeded, "proc-ref", null)));
        var payment = Assert.IsType<PatientPayment>(created.Value);

        var actions = await db.AuditEvents.Where(e => e.ObjectId == payment.Id).Select(e => e.Action).ToListAsync();
        Assert.Contains("patient_payment.attempted", actions);
    }

    [Fact]
    public async Task OutcomeScore_RecordAndUpdate_BothAudited()
    {
        var (db, org, patient, _) = await SeedAsync();
        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist };
        patient.AssignedTherapistId = therapist.UserId;
        await db.SaveChangesAsync();
        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var service = new OutcomeScoreService(db, tenantAccess, audit);

        var measuredOn = DateOnly.FromDateTime(DateTime.UtcNow);
        var recorded = await service.RecordAsync(
            new RecordOutcomeScoreRequest(patient.Id, null, OutcomeMeasure.Lefs, measuredOn, 60, 80, null), therapist);
        await service.RecordAsync(
            new RecordOutcomeScoreRequest(patient.Id, null, OutcomeMeasure.Lefs, measuredOn, 65, 80, null), therapist);

        var actions = await db.AuditEvents.Where(e => e.ObjectId == recorded.Id).Select(e => e.Action).ToListAsync();
        Assert.Contains("outcome_score.recorded", actions);
        Assert.Contains("outcome_score.updated", actions);
    }
}
