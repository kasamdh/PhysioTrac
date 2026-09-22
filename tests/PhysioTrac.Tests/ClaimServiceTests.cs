using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Billing;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;
using Xunit;

namespace PhysioTrac.Tests;

/// <summary>Mirrors `Claim`'s creation rules and the totals logic from
/// `care/models.py` (`total_charge_amount`/`total_paid`/`total_adjusted`/`balance`).</summary>
public class ClaimServiceTests
{
    private static (PhysioTracDbContext Db, ClaimService Claims, ClaimTransactionService Transactions, Organization Org, Patient Patient, Payer Payer, PatientInsurance Insurance, TestCurrentUser Biller)
        NewServices()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var patient = new Patient { OrganizationId = org.Id, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1) };
        var payer = new Payer { OrganizationId = org.Id, Name = "Acme Insurance" };
        var insurance = new PatientInsurance
        {
            OrganizationId = org.Id, PatientId = patient.Id, PayerId = payer.Id, MemberId = "M1",
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)),
        };
        db.Organizations.Add(org);
        db.Patients.Add(patient);
        db.Payers.Add(payer);
        db.PatientInsurancePolicies.Add(insurance);
        db.SaveChanges();

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var biller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Biller };
        return (db, new ClaimService(db, tenantAccess), new ClaimTransactionService(db, tenantAccess), org, patient, payer, insurance, biller);
    }

    private static Charge NewCharge(Guid orgId, Guid patientId, decimal amount) => new()
    {
        OrganizationId = orgId, PatientId = patientId, ProviderId = Guid.NewGuid(),
        ServiceDate = DateOnly.FromDateTime(DateTime.UtcNow), CptCode = "97110", ChargeAmount = amount,
    };

    [Fact]
    public async Task CreateFromCharges_LocksChargesToClaim()
    {
        var (db, claims, _, org, patient, _, insurance, biller) = NewServices();
        var charge = NewCharge(org.Id, patient.Id, 100m);
        db.Charges.Add(charge);
        await db.SaveChangesAsync();

        var claim = await claims.CreateFromChargesAsync(new CreateClaimRequest(patient.Id, insurance.Id, new[] { charge.Id }), biller);

        var reloaded = await db.Charges.FirstAsync(c => c.Id == charge.Id);
        Assert.Equal(claim.Id, reloaded.ClaimId);
    }

    [Fact]
    public async Task CreateFromCharges_AlreadyClaimedCharge_Throws()
    {
        var (db, claims, _, org, patient, _, insurance, biller) = NewServices();
        var charge = NewCharge(org.Id, patient.Id, 100m);
        db.Charges.Add(charge);
        await db.SaveChangesAsync();
        await claims.CreateFromChargesAsync(new CreateClaimRequest(patient.Id, insurance.Id, new[] { charge.Id }), biller);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            claims.CreateFromChargesAsync(new CreateClaimRequest(patient.Id, insurance.Id, new[] { charge.Id }), biller));
    }

    [Fact]
    public async Task CreateFromCharges_NoCharges_Throws()
    {
        var (_, claims, _, _, patient, _, insurance, biller) = NewServices();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            claims.CreateFromChargesAsync(new CreateClaimRequest(patient.Id, insurance.Id, Array.Empty<Guid>()), biller));
    }

    [Fact]
    public async Task Totals_ComputesFromChargesAndTransactions()
    {
        var (db, claims, transactions, org, patient, _, insurance, biller) = NewServices();
        var chargeA = NewCharge(org.Id, patient.Id, 100m);
        var chargeB = NewCharge(org.Id, patient.Id, 50m);
        db.Charges.AddRange(chargeA, chargeB);
        await db.SaveChangesAsync();
        var claim = await claims.CreateFromChargesAsync(new CreateClaimRequest(patient.Id, insurance.Id, new[] { chargeA.Id, chargeB.Id }), biller);

        await transactions.RecordAsync(new RecordClaimTransactionRequest(
            patient.Id, claim.Id, null, ClaimTransactionKind.InsurancePayment, ClaimTransactionMethod.Eft, 120m, null, null, null, null, null), biller);
        await transactions.RecordAsync(new RecordClaimTransactionRequest(
            patient.Id, claim.Id, null, ClaimTransactionKind.WriteOff, null, 10m, null, null, null, null, null), biller);

        var totals = await claims.GetTotalsAsync(claim.Id, biller);

        Assert.Equal(150m, totals.TotalChargeAmount);
        Assert.Equal(120m, totals.TotalPaid);
        Assert.Equal(10m, totals.TotalAdjusted);
        Assert.Equal(20m, totals.Balance);
    }

    [Fact]
    public async Task Transaction_TransferWithoutDestination_Throws()
    {
        var (_, _, transactions, _, patient, _, _, biller) = NewServices();

        await Assert.ThrowsAsync<InvalidOperationException>(() => transactions.RecordAsync(
            new RecordClaimTransactionRequest(patient.Id, null, null, ClaimTransactionKind.Transfer, null, 50m, null, null, null, null, null), biller));
    }

    [Fact]
    public async Task Transaction_TransferToSameClaim_Throws()
    {
        var (db, claims, transactions, org, patient, _, insurance, biller) = NewServices();
        var charge = NewCharge(org.Id, patient.Id, 100m);
        db.Charges.Add(charge);
        await db.SaveChangesAsync();
        var claim = await claims.CreateFromChargesAsync(new CreateClaimRequest(patient.Id, insurance.Id, new[] { charge.Id }), biller);

        await Assert.ThrowsAsync<InvalidOperationException>(() => transactions.RecordAsync(
            new RecordClaimTransactionRequest(patient.Id, claim.Id, claim.Id, ClaimTransactionKind.Transfer, null, 50m, null, null, null, null, null), biller));
    }

    [Fact]
    public async Task Transaction_NonPositiveAmount_Throws()
    {
        var (_, _, transactions, _, patient, _, _, biller) = NewServices();

        await Assert.ThrowsAsync<InvalidOperationException>(() => transactions.RecordAsync(
            new RecordClaimTransactionRequest(patient.Id, null, null, ClaimTransactionKind.PatientPayment, null, 0m, null, null, null, null, null), biller));
    }
}
