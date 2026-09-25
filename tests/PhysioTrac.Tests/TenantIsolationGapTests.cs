using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Api.Controllers;
using PhysioTrac.Application.Billing;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Phase 9's tenant-isolation audit found 12 controllers with zero
/// isolation-test coverage of any kind (most of the others in this app
/// already had one from the phase that introduced them). One cross-org
/// case per controller here, closing every gap the audit found -- not a
/// re-test of RequirePatientAccessAsync/OrganizationRequiredAsync's own
/// logic, which TenantAccessServiceTests already covers exhaustively; this
/// is proof each of these specific controllers actually calls one of them.</summary>
public class TenantIsolationGapTests
{
    private static PhysioTracDbContext NewDb() => new(
        new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ControllerContext NewContext() => new() { HttpContext = new DefaultHttpContext() };

    private static async Task<(PhysioTracDbContext Db, Organization Org1000, Organization Org1001, Patient PatientIn1001, TestCurrentUser Org1000Admin)> SeedAsync()
    {
        var db = NewDb();
        var org1000 = new Organization { Name = "Org 1000", Slug = "org-1000", ClientNumber = 1000 };
        var org1001 = new Organization { Name = "Org 1001", Slug = "org-1001", ClientNumber = 1001 };
        var patientIn1001 = new Patient { OrganizationId = org1001.Id, FirstName = "Beta", LastName = "Brown", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Organizations.AddRange(org1000, org1001);
        db.Patients.Add(patientIn1001);
        await db.SaveChangesAsync();
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        return (db, org1000, org1001, patientIn1001, admin);
    }

    [Fact]
    public async Task ClaimDenials_ForAnotherOrganizationsClaim_ThrowsNotFound()
    {
        var (db, org1000, org1001, patientIn1001, admin) = await SeedAsync();
        var claimIn1001 = new Claim { OrganizationId = org1001.Id, PatientId = patientIn1001.Id, PatientInsuranceId = Guid.NewGuid(), PayerId = Guid.NewGuid() };
        db.Claims.Add(claimIn1001);
        await db.SaveChangesAsync();
        var controller = new ClaimDenialsController(new TenantAccessService(db, new AuditService(db)), admin, db) { ControllerContext = NewContext() };

        var result = Assert.IsType<NotFoundObjectResult>(
            await controller.Create(new CreateClaimDenialRequest(claimIn1001.Id, null, "Missing authorization", null, null, null)));
        Assert.Empty(await db.ClaimDenials.ToListAsync());
    }

    [Fact]
    public async Task ClaimTransactions_ForAnotherOrganizationsPatient_ThrowsNotFound()
    {
        var (db, org1000, org1001, patientIn1001, admin) = await SeedAsync();
        var audit = new AuditService(db);
        var service = new ClaimTransactionService(db, new TenantAccessService(db, audit));

        await Assert.ThrowsAsync<NotFoundException>(() => service.RecordAsync(
            new RecordClaimTransactionRequest(patientIn1001.Id, null, null, ClaimTransactionKind.PatientPayment, ClaimTransactionMethod.Cash, 50m, null, null, null, null, null),
            admin));
        Assert.Empty(await db.ClaimTransactions.ToListAsync());
    }

    [Fact]
    public async Task CptCodeMappings_CreatedInOneOrganization_NeverVisibleToAnother()
    {
        var (db, org1000, org1001, _, admin) = await SeedAsync();
        var controller1001 = new CptCodeMappingsController(new TenantAccessService(db, new AuditService(db)),
            new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1001.Id, Role = UserRole.Admin }, db)
        { ControllerContext = NewContext() };
        await controller1001.Create(new CreateCptCodeMappingRequest(InterventionCategory.TherapeuticExercise, "97110"));

        var controller1000 = new CptCodeMappingsController(new TenantAccessService(db, new AuditService(db)), admin, db) { ControllerContext = NewContext() };
        var result = Assert.IsType<OkObjectResult>(await controller1000.List());
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<CptCodeMapping>>(result.Value));
    }

    [Fact]
    public async Task PayerFeeSchedule_ForAnotherOrganizationsPayer_ThrowsNotFound()
    {
        var (db, org1000, org1001, _, admin) = await SeedAsync();
        var payerIn1001 = new Payer { OrganizationId = org1001.Id, Name = "Aetna" };
        db.Payers.Add(payerIn1001);
        await db.SaveChangesAsync();
        var controller = new PayerFeeScheduleController(new TenantAccessService(db, new AuditService(db)), admin, db) { ControllerContext = NewContext() };

        var result = await controller.List(payerIn1001.Id);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Payers_CreatedInOneOrganization_NeverVisibleToAnother()
    {
        var (db, org1000, org1001, _, admin) = await SeedAsync();
        var payerIn1001 = new Payer { OrganizationId = org1001.Id, Name = "Aetna" };
        db.Payers.Add(payerIn1001);
        await db.SaveChangesAsync();
        var controller = new PayersController(new TenantAccessService(db, new AuditService(db)), admin, db) { ControllerContext = NewContext() };

        var result = Assert.IsType<OkObjectResult>(await controller.List());
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<Payer>>(result.Value));
    }

    [Fact]
    public async Task ReferringProviders_CreatedInOneOrganization_NeverVisibleToAnother()
    {
        var (db, org1000, org1001, _, admin) = await SeedAsync();
        db.ReferringProviders.Add(new ReferringProvider { OrganizationId = org1001.Id, FirstName = "Nadia", LastName = "Farouk" });
        await db.SaveChangesAsync();
        var controller = new ReferringProvidersController(new TenantAccessService(db, new AuditService(db)), admin, db) { ControllerContext = NewContext() };

        var result = Assert.IsType<OkObjectResult>(await controller.List());
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ReferringProviderDto>>(result.Value));
    }

    [Fact]
    public async Task ServicePrices_CreatedInOneOrganization_NeverVisibleToAnother()
    {
        var (db, org1000, org1001, _, admin) = await SeedAsync();
        db.ServicePrices.Add(new ServicePrice { OrganizationId = org1001.Id, CptCode = "97110", Label = "Therapeutic Exercise", Price = 65m });
        await db.SaveChangesAsync();
        var controller = new ServicePricesController(new TenantAccessService(db, new AuditService(db)), admin, db) { ControllerContext = NewContext() };

        var result = Assert.IsType<OkObjectResult>(await controller.List(null));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ServicePrice>>(result.Value));
    }

    [Fact]
    public async Task AppointmentTypes_UpdateBilling_ForAnotherOrganizationsType_ThrowsNotFound()
    {
        var (db, org1000, org1001, _, admin) = await SeedAsync();
        var typeIn1001 = new AppointmentType { OrganizationId = org1001.Id, Name = "Follow-up" };
        db.AppointmentTypes.Add(typeIn1001);
        await db.SaveChangesAsync();
        var controller = new AppointmentTypesController(new TenantAccessService(db, new AuditService(db)), admin, db) { ControllerContext = NewContext() };

        var result = await controller.UpdateBilling(typeIn1001.Id, new UpdateAppointmentTypeBillingRequest("97110", 95m));
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task PatientInsurance_ForAnotherOrganizationsPatient_ThrowsForbidden()
    {
        var (db, org1000, org1001, patientIn1001, admin) = await SeedAsync();
        var payerIn1000 = new Payer { OrganizationId = org1000.Id, Name = "BCBS" };
        db.Payers.Add(payerIn1000);
        await db.SaveChangesAsync();
        var controller = new PatientInsuranceController(new TenantAccessService(db, new AuditService(db)), admin, db) { ControllerContext = NewContext() };

        var result = Assert.IsType<ObjectResult>(await controller.Create(new CreatePatientInsuranceRequest(
            patientIn1001.Id, payerIn1000.Id, InsuranceRank.Primary, null, "MEM-1", null, null, null,
            RelationshipToSubscriber.Self, DateOnly.FromDateTime(DateTime.UtcNow), null, null, null, null, false)));
        Assert.Equal(403, result.StatusCode);
        Assert.Empty(await db.PatientInsurancePolicies.ToListAsync());
    }

    [Fact]
    public async Task Superbills_ForAnotherOrganizationsPatient_ThrowsForbidden()
    {
        var (db, org1000, org1001, patientIn1001, admin) = await SeedAsync();
        var billerIn1000 = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Biller };
        var audit = new AuditService(db);
        var controller = new SuperbillsController(new TenantAccessService(db, audit), billerIn1000, db, audit) { ControllerContext = NewContext() };

        var result = Assert.IsType<ObjectResult>(await controller.Create(
            new CreateSuperbillRequest(patientIn1001.Id, Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), null, 100m, null)));
        Assert.Equal(403, result.StatusCode);
        Assert.Empty(await db.Superbills.ToListAsync());
    }

    [Fact]
    public async Task PaymentRecords_ForAnotherOrganizationsPatient_ThrowsForbidden()
    {
        var (db, org1000, org1001, patientIn1001, admin) = await SeedAsync();
        var billerIn1000 = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Biller };
        var audit = new AuditService(db);
        var controller = new PaymentRecordsController(new TenantAccessService(db, audit), billerIn1000, db, audit) { ControllerContext = NewContext() };

        var result = Assert.IsType<ObjectResult>(await controller.Create(
            new CreatePaymentRecordRequest(patientIn1001.Id, null, 50m, null, ClaimTransactionMethod.Cash, "ref")));
        Assert.Equal(403, result.StatusCode);
        Assert.Empty(await db.PaymentRecords.ToListAsync());
    }
}
