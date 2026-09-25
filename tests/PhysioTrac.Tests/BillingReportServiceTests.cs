using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Billing;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

public class BillingReportServiceTests
{
    private static (PhysioTracDbContext Db, BillingReportService Service, Organization Org, Patient Patient, TestCurrentUser Biller)
        NewService()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var patient = new Patient { OrganizationId = org.Id, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Organizations.Add(org);
        db.Patients.Add(patient);
        db.SaveChanges();

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var biller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Biller };
        return (db, new BillingReportService(db, tenantAccess), org, patient, biller);
    }

    private static Charge NewCharge(Organization org, Patient patient, Guid providerId, DateOnly serviceDate, string cptCode, decimal amount, Guid? claimId = null, Guid? locationId = null) => new()
    {
        OrganizationId = org.Id,
        PatientId = patient.Id,
        ProviderId = providerId,
        LocationId = locationId,
        ServiceDate = serviceDate,
        CptCode = cptCode,
        ChargeAmount = amount,
        ClaimId = claimId,
    };

    [Fact]
    public async Task GetAgingReport_BucketsAnOutstandingClaimByDaysSinceItsEarliestChargeServiceDate()
    {
        var (db, service, org, patient, biller) = NewService();
        var providerId = Guid.NewGuid();

        var claim = new Claim { OrganizationId = org.Id, PatientId = patient.Id, PatientInsuranceId = Guid.NewGuid(), PayerId = Guid.NewGuid(), Status = ClaimStatus.Submitted };
        db.Claims.Add(claim);
        // 45 days old -> the 31-60 bucket.
        db.Charges.Add(NewCharge(org, patient, providerId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-45)), "97110", 100m, claim.Id));
        await db.SaveChangesAsync();

        var report = await service.GetAgingReportAsync(biller);

        Assert.Equal(100m, report.InsuranceClaims.Days31To60);
        Assert.Equal(0m, report.InsuranceClaims.Days0To30);
        Assert.Equal(100m, report.Combined.Total);
    }

    [Fact]
    public async Task GetAgingReport_FullyPaidClaim_ContributesNothing()
    {
        var (db, service, org, patient, biller) = NewService();
        var providerId = Guid.NewGuid();
        var claim = new Claim { OrganizationId = org.Id, PatientId = patient.Id, PatientInsuranceId = Guid.NewGuid(), PayerId = Guid.NewGuid(), Status = ClaimStatus.Paid };
        db.Claims.Add(claim);
        db.Charges.Add(NewCharge(org, patient, providerId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-100)), "97110", 100m, claim.Id));
        await db.SaveChangesAsync();
        db.ClaimTransactions.Add(new ClaimTransaction { OrganizationId = org.Id, PatientId = patient.Id, ClaimId = claim.Id, Kind = ClaimTransactionKind.InsurancePayment, Amount = 100m });
        await db.SaveChangesAsync();

        var report = await service.GetAgingReportAsync(biller);

        Assert.Equal(0m, report.Combined.Total);
    }

    [Fact]
    public async Task GetAgingReport_OutstandingSuperbill_BucketsByItsOwnServiceDate()
    {
        var (db, service, org, patient, biller) = NewService();
        var superbill = new Superbill { PatientId = patient.Id, ClinicianId = Guid.NewGuid(), ServiceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-95)), Amount = 80m };
        db.Superbills.Add(superbill);
        await db.SaveChangesAsync();

        var report = await service.GetAgingReportAsync(biller);

        Assert.Equal(80m, report.CashPaySuperbills.Days90Plus);
        Assert.Equal(80m, report.Combined.Total);
    }

    [Fact]
    public async Task GetPatientBalance_SumsOpenClaimAndSuperbillBalances()
    {
        var (db, service, org, patient, biller) = NewService();
        var providerId = Guid.NewGuid();
        var claim = new Claim { OrganizationId = org.Id, PatientId = patient.Id, PatientInsuranceId = Guid.NewGuid(), PayerId = Guid.NewGuid(), Status = ClaimStatus.Submitted };
        db.Claims.Add(claim);
        db.Charges.Add(NewCharge(org, patient, providerId, DateOnly.FromDateTime(DateTime.UtcNow), "97110", 120m, claim.Id));
        db.Superbills.Add(new Superbill { PatientId = patient.Id, ClinicianId = Guid.NewGuid(), ServiceDate = DateOnly.FromDateTime(DateTime.UtcNow), Amount = 50m });
        await db.SaveChangesAsync();

        var balance = await service.GetPatientBalanceAsync(patient.Id, biller);

        Assert.Equal(120m, balance.OpenClaimsBalance);
        Assert.Equal(50m, balance.OpenSuperbillsBalance);
        Assert.Equal(170m, balance.TotalBalance);
    }

    [Fact]
    public async Task GetPatientBalance_AnotherOrganizationsPatient_ThrowsForbidden()
    {
        var (db, service, org, patient, biller) = NewService();
        var otherOrg = new Organization { Name = "Client B", Slug = "client-b" };
        db.Organizations.Add(otherOrg);
        await db.SaveChangesAsync();
        var otherOrgBiller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = otherOrg.Id, Role = UserRole.Biller };

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetPatientBalanceAsync(patient.Id, otherOrgBiller));
    }

    [Fact]
    public async Task GetRevenueReport_GroupsByProvider_AndComputesTotalBilledAndCollected()
    {
        var (db, service, org, patient, biller) = NewService();
        var providerA = new Provider { OrganizationId = org.Id, FirstName = "Jamie", LastName = "Chen" };
        var providerB = new Provider { OrganizationId = org.Id, FirstName = "Avery", LastName = "Kim" };
        db.Providers.AddRange(providerA, providerB);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Charges.AddRange(
            NewCharge(org, patient, providerA.Id, today, "97110", 100m),
            NewCharge(org, patient, providerA.Id, today, "97140", 50m),
            NewCharge(org, patient, providerB.Id, today, "97110", 75m));
        db.PaymentRecords.Add(new PaymentRecord { PatientId = patient.Id, RecordedById = biller.UserId, Amount = 60m, ReceivedOn = today, Status = PaymentRecordStatus.Received });
        await db.SaveChangesAsync();

        var report = await service.GetRevenueReportAsync(
            new RevenueReportFilter(today.AddDays(-1), today.AddDays(1), null, null, null, RevenueGroupBy.Provider), biller);

        Assert.Equal(225m, report.TotalBilled);
        Assert.Equal(60m, report.TotalCollected);
        Assert.Equal(2, report.Rows.Count);
        var jamieRow = Assert.Single(report.Rows, r => r.Key == "Jamie Chen");
        Assert.Equal(150m, jamieRow.BilledAmount);
        Assert.Equal(2, jamieRow.ChargeCount);
    }

    [Fact]
    public async Task GetRevenueReport_FiltersOutChargesOutsideTheDateRange()
    {
        var (db, service, org, patient, biller) = NewService();
        var provider = new Provider { OrganizationId = org.Id, FirstName = "Jamie", LastName = "Chen" };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Charges.AddRange(
            NewCharge(org, patient, provider.Id, today, "97110", 100m),
            NewCharge(org, patient, provider.Id, today.AddDays(-60), "97110", 999m));
        await db.SaveChangesAsync();

        var report = await service.GetRevenueReportAsync(
            new RevenueReportFilter(today.AddDays(-7), today, null, null, null, RevenueGroupBy.Service), biller);

        Assert.Equal(100m, report.TotalBilled);
    }

    [Fact]
    public async Task GetRevenueReport_VoidCharges_AreExcluded()
    {
        var (db, service, org, patient, biller) = NewService();
        var provider = new Provider { OrganizationId = org.Id, FirstName = "Jamie", LastName = "Chen" };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var voided = NewCharge(org, patient, provider.Id, today, "97110", 999m);
        voided.Status = ChargeStatus.Void;
        db.Charges.Add(voided);
        await db.SaveChangesAsync();

        var report = await service.GetRevenueReportAsync(
            new RevenueReportFilter(today.AddDays(-1), today, null, null, null, RevenueGroupBy.Service), biller);

        Assert.Equal(0m, report.TotalBilled);
        Assert.Empty(report.Rows);
    }

    [Fact]
    public async Task GetStatementLineItems_ExcludesActivityAfterTheStatementDate()
    {
        var (db, service, org, patient, biller) = NewService();
        var provider = new Provider { OrganizationId = org.Id, FirstName = "Jamie", LastName = "Chen" };
        db.Providers.Add(provider);
        var statement = new PatientStatement { OrganizationId = org.Id, PatientId = patient.Id, StatementDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), DueDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        db.PatientStatements.Add(statement);
        await db.SaveChangesAsync();

        db.Charges.AddRange(
            NewCharge(org, patient, provider.Id, statement.StatementDate.AddDays(-1), "97110", 100m), // before -- included
            NewCharge(org, patient, provider.Id, statement.StatementDate.AddDays(1), "97110", 200m)); // after -- excluded
        await db.SaveChangesAsync();

        var items = await service.GetStatementLineItemsAsync(statement.Id, biller);

        var item = Assert.Single(items);
        Assert.Equal(100m, item.Amount);
    }

    [Fact]
    public async Task GetStatementLineItems_RefundIncreasesTheShownBalance_PaymentsReduceIt()
    {
        var (db, service, org, patient, biller) = NewService();
        var claim = new Claim { OrganizationId = org.Id, PatientId = patient.Id, PatientInsuranceId = Guid.NewGuid(), PayerId = Guid.NewGuid() };
        db.Claims.Add(claim);
        var statement = new PatientStatement { OrganizationId = org.Id, PatientId = patient.Id, StatementDate = DateOnly.FromDateTime(DateTime.UtcNow), DueDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        db.PatientStatements.Add(statement);
        await db.SaveChangesAsync();
        db.ClaimTransactions.AddRange(
            new ClaimTransaction { OrganizationId = org.Id, PatientId = patient.Id, ClaimId = claim.Id, Kind = ClaimTransactionKind.InsurancePayment, Amount = 100m, PaymentDate = statement.StatementDate },
            new ClaimTransaction { OrganizationId = org.Id, PatientId = patient.Id, ClaimId = claim.Id, Kind = ClaimTransactionKind.Refund, Amount = 30m, PaymentDate = statement.StatementDate });
        await db.SaveChangesAsync();

        var items = await service.GetStatementLineItemsAsync(statement.Id, biller);

        Assert.Contains(items, i => i.Kind == "InsurancePayment" && i.Amount == -100m);
        Assert.Contains(items, i => i.Kind == "Refund" && i.Amount == 30m);
    }
}
