using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Billing;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

public class BillingReportService : IBillingReportService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;

    public BillingReportService(PhysioTracDbContext db, ITenantAccessService tenantAccess)
    {
        _db = db;
        _tenantAccess = tenantAccess;
    }

    public async Task<PatientBalanceDto> GetPatientBalanceAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);

        var claims = await _db.Claims.Where(c => c.PatientId == patient.Id).Select(c => c.Id).ToListAsync(ct);
        decimal claimsBalance = 0;
        foreach (var claimId in claims)
        {
            claimsBalance += Math.Max(0, await ComputeClaimBalanceAsync(claimId, ct));
        }

        var superbills = await _db.Superbills.Where(s => s.PatientId == patient.Id).ToListAsync(ct);
        decimal superbillsBalance = 0;
        foreach (var superbill in superbills)
        {
            superbillsBalance += Math.Max(0, await ComputeSuperbillBalanceAsync(superbill, ct));
        }

        return new PatientBalanceDto(patient.Id, claimsBalance, superbillsBalance, claimsBalance + superbillsBalance);
    }

    public async Task<AgingReportDto> GetAgingReportAsync(ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Billing);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var claims = await _db.Claims.Where(c => c.OrganizationId == organization.Id).ToListAsync(ct);
        var claimBucket = new decimal[4];
        foreach (var claim in claims)
        {
            var balance = await ComputeClaimBalanceAsync(claim.Id, ct);
            if (balance <= 0) continue;

            var earliestServiceDate = await _db.Charges.Where(c => c.ClaimId == claim.Id).MinAsync(c => (DateOnly?)c.ServiceDate, ct);
            if (earliestServiceDate is null) continue;
            BucketInto(claimBucket, earliestServiceDate.Value, today, balance);
        }

        // Superbill has no OrganizationId of its own -- scope it via its patient.
        var superbills = await _db.Superbills
            .Join(_db.Patients.Where(p => p.OrganizationId == organization.Id), s => s.PatientId, p => p.Id, (s, p) => s)
            .ToListAsync(ct);
        var superbillBucket = new decimal[4];
        foreach (var superbill in superbills)
        {
            var balance = await ComputeSuperbillBalanceAsync(superbill, ct);
            if (balance <= 0) continue;
            BucketInto(superbillBucket, superbill.ServiceDate, today, balance);
        }

        var combined = new[]
        {
            claimBucket[0] + superbillBucket[0], claimBucket[1] + superbillBucket[1],
            claimBucket[2] + superbillBucket[2], claimBucket[3] + superbillBucket[3],
        };

        return new AgingReportDto(ToBucketDto(claimBucket), ToBucketDto(superbillBucket), ToBucketDto(combined));
    }

    public async Task<RevenueReportDto> GetRevenueReportAsync(RevenueReportFilter filter, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Billing);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        if (filter.To < filter.From)
        {
            throw new InvalidOperationException("The end of the date range cannot precede the start.");
        }

        var charges = _db.Charges.Where(c =>
            c.OrganizationId == organization.Id && c.Status != ChargeStatus.Void &&
            c.ServiceDate >= filter.From && c.ServiceDate <= filter.To);
        if (filter.ProviderId is Guid providerId) charges = charges.Where(c => c.ProviderId == providerId);
        if (filter.LocationId is Guid locationId) charges = charges.Where(c => c.LocationId == locationId);
        if (!string.IsNullOrWhiteSpace(filter.CptCode)) charges = charges.Where(c => c.CptCode == filter.CptCode);

        var chargeList = await charges.ToListAsync(ct);
        var totalBilled = chargeList.Sum(c => c.ChargeAmount);

        List<RevenueReportRowDto> rows;
        switch (filter.GroupBy)
        {
            case RevenueGroupBy.Provider:
                var providerIds = chargeList.Select(c => c.ProviderId).Distinct().ToList();
                var providers = await _db.Providers.Where(p => providerIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
                rows = chargeList.GroupBy(c => c.ProviderId)
                    .Select(g => new RevenueReportRowDto(
                        providers.TryGetValue(g.Key, out var p) ? $"{p.FirstName} {p.LastName}" : "Unknown provider",
                        g.Sum(c => c.ChargeAmount), g.Count()))
                    .OrderByDescending(r => r.BilledAmount).ToList();
                break;
            case RevenueGroupBy.Location:
                var locationIds = chargeList.Where(c => c.LocationId is not null).Select(c => c.LocationId!.Value).Distinct().ToList();
                var locations = await _db.Locations.Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
                rows = chargeList.GroupBy(c => c.LocationId)
                    .Select(g => new RevenueReportRowDto(
                        g.Key is Guid locId && locations.TryGetValue(locId, out var l) ? l.Name : "No location recorded",
                        g.Sum(c => c.ChargeAmount), g.Count()))
                    .OrderByDescending(r => r.BilledAmount).ToList();
                break;
            default: // Service
                rows = chargeList.GroupBy(c => c.CptCode)
                    .Select(g => new RevenueReportRowDto(g.Key, g.Sum(c => c.ChargeAmount), g.Count()))
                    .OrderByDescending(r => r.BilledAmount).ToList();
                break;
        }

        var insuranceCollected = await _db.ClaimTransactions.Where(t =>
                t.OrganizationId == organization.Id && t.PaymentDate >= filter.From && t.PaymentDate <= filter.To)
            .SumAsync(t => t.Kind == ClaimTransactionKind.Refund ? -t.Amount
                : t.Kind == ClaimTransactionKind.InsurancePayment || t.Kind == ClaimTransactionKind.PatientPayment ? t.Amount : 0m, ct);
        var cashPayCollected = await _db.PaymentRecords
            .Join(_db.Patients.Where(p => p.OrganizationId == organization.Id), r => r.PatientId, p => p.Id, (r, p) => r)
            .Where(r => r.Status == PaymentRecordStatus.Received && r.ReceivedOn >= filter.From && r.ReceivedOn <= filter.To)
            .SumAsync(r => r.Amount, ct);

        return new RevenueReportDto(filter.From, filter.To, totalBilled, insuranceCollected + cashPayCollected, rows);
    }

    public async Task<IReadOnlyList<PatientStatementLineItemDto>> GetStatementLineItemsAsync(Guid statementId, ICurrentUser actor, CancellationToken ct = default)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var statement = await _db.PatientStatements.FirstOrDefaultAsync(s => s.Id == statementId, ct)
            ?? throw new NotFoundException("Statement was not found.");
        if (statement.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Statement was not found.");
        }
        await _tenantAccess.RequirePatientAccessAsync(actor, statement.PatientId, ct: ct);

        var items = new List<PatientStatementLineItemDto>();

        var charges = await _db.Charges.Where(c => c.PatientId == statement.PatientId && c.ServiceDate <= statement.StatementDate).ToListAsync(ct);
        items.AddRange(charges.Select(c => new PatientStatementLineItemDto(c.ServiceDate, "Charge", $"{c.CptCode} x{c.Units}", c.ChargeAmount)));

        var transactionCutoff = statement.StatementDate;
        var transactions = await _db.ClaimTransactions
            .Where(t => t.PatientId == statement.PatientId && t.PaymentDate <= transactionCutoff)
            .ToListAsync(ct);
        // A refund gives money back, so it increases what's owed again;
        // every other kind (payments, adjustments, write-offs, transfers
        // out of this claim) reduces this claim's balance.
        items.AddRange(transactions.Select(t => new PatientStatementLineItemDto(
            t.PaymentDate, t.Kind.ToString(), t.Reference ?? t.Kind.ToString(),
            t.Kind == ClaimTransactionKind.Refund ? t.Amount : -t.Amount)));

        var payments = await _db.PaymentRecords
            .Where(p => p.PatientId == statement.PatientId && p.Status == PaymentRecordStatus.Received && p.ReceivedOn <= transactionCutoff)
            .ToListAsync(ct);
        items.AddRange(payments.Select(p => new PatientStatementLineItemDto(p.ReceivedOn, "Payment", p.PaymentProcessorReference, -p.Amount)));

        return items.OrderBy(i => i.Date).ToList();
    }

    private async Task<decimal> ComputeClaimBalanceAsync(Guid claimId, CancellationToken ct)
    {
        var totalCharge = await _db.Charges.Where(c => c.ClaimId == claimId).SumAsync(c => c.ChargeAmount, ct);
        var transactions = await _db.ClaimTransactions.Where(t => t.ClaimId == claimId).ToListAsync(ct);
        var payments = transactions.Where(t => t.Kind is ClaimTransactionKind.InsurancePayment or ClaimTransactionKind.PatientPayment).Sum(t => t.Amount);
        var refunds = transactions.Where(t => t.Kind == ClaimTransactionKind.Refund).Sum(t => t.Amount);
        var adjusted = transactions.Where(t => t.Kind is ClaimTransactionKind.Adjustment or ClaimTransactionKind.WriteOff).Sum(t => t.Amount);
        return totalCharge - (payments - refunds) - adjusted;
    }

    private async Task<decimal> ComputeSuperbillBalanceAsync(Superbill superbill, CancellationToken ct)
    {
        var received = await _db.PaymentRecords
            .Where(p => p.SuperbillId == superbill.Id && p.Status == PaymentRecordStatus.Received)
            .SumAsync(p => p.Amount, ct);
        return superbill.Amount - received;
    }

    private static void BucketInto(decimal[] bucket, DateOnly serviceDate, DateOnly today, decimal amount)
    {
        var ageDays = today.DayNumber - serviceDate.DayNumber;
        var index = ageDays switch
        {
            <= 30 => 0,
            <= 60 => 1,
            <= 90 => 2,
            _ => 3,
        };
        bucket[index] += amount;
    }

    private static AgingBucketDto ToBucketDto(IReadOnlyList<decimal> bucket) =>
        new(bucket[0], bucket[1], bucket[2], bucket[3], bucket[0] + bucket[1] + bucket[2] + bucket[3]);
}
