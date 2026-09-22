using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Billing;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Direct port of the `Claim` creation rules and totals logic from
/// `care/models.py`.</summary>
public class ClaimService : IClaimService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;

    public ClaimService(PhysioTracDbContext db, ITenantAccessService tenantAccess)
    {
        _db = db;
        _tenantAccess = tenantAccess;
    }

    public async Task<Claim> CreateFromChargesAsync(CreateClaimRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Billing);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId && p.OrganizationId == organization.Id, ct)
            ?? throw new NotFoundException("Patient was not found.");

        var patientInsurance = await _db.PatientInsurancePolicies.FirstOrDefaultAsync(
                i => i.Id == request.PatientInsuranceId && i.OrganizationId == organization.Id, ct)
            ?? throw new NotFoundException("Insurance policy was not found.");
        if (patientInsurance.PatientId != patient.Id)
        {
            throw new InvalidOperationException("Insurance policy must belong to this patient.");
        }

        if (request.ChargeIds.Count == 0)
        {
            throw new InvalidOperationException("This claim has no charges attached.");
        }
        var charges = await _db.Charges.Include(c => c.DiagnosisCodes)
            .Where(c => request.ChargeIds.Contains(c.Id) && c.OrganizationId == organization.Id)
            .ToListAsync(ct);
        if (charges.Count != request.ChargeIds.Count)
        {
            throw new NotFoundException("One or more charges were not found.");
        }
        foreach (var charge in charges)
        {
            if (charge.PatientId != patient.Id)
            {
                throw new InvalidOperationException("Every charge must belong to this patient.");
            }
            if (charge.Status == ChargeStatus.Void)
            {
                throw new InvalidOperationException("A void charge cannot be added to a claim.");
            }
            if (charge.ClaimId is not null || charge.SuperbillId is not null)
            {
                throw new InvalidOperationException("A charge can be billed to insurance or cash-pay, not both, and cannot be reused across claims.");
            }
        }

        var diagnosisCodeList = charges.SelectMany(c => c.DiagnosisCodes.Select(d => d.Code)).Distinct().ToList();
        if (diagnosisCodeList.Count > 12)
        {
            throw new InvalidOperationException("A claim can carry at most 12 diagnosis codes.");
        }

        var claim = new Claim
        {
            OrganizationId = organization.Id,
            PatientId = patient.Id,
            PatientInsuranceId = patientInsurance.Id,
            PayerId = patientInsurance.PayerId,
            DiagnosisCodeList = diagnosisCodeList,
            Status = ClaimStatus.Draft,
            CreatedById = actor.UserId,
        };
        _db.Claims.Add(claim);
        await _db.SaveChangesAsync(ct);

        foreach (var charge in charges)
        {
            charge.ClaimId = claim.Id;
        }
        await _db.SaveChangesAsync(ct);

        return claim;
    }

    public async Task<Claim> UpdateStatusAsync(Guid claimId, UpdateClaimStatusRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Billing);
        var claim = await LoadClaimInOrgAsync(claimId, actor, ct);

        claim.Status = request.Status;
        if (request.Status == ClaimStatus.Submitted && claim.SubmittedAt is null)
        {
            claim.SubmittedAt = DateTimeOffset.UtcNow;
        }
        if (Claim.TerminalStatuses.Contains(request.Status) && claim.ClosedAt is null)
        {
            claim.ClosedAt = DateTimeOffset.UtcNow;
        }
        claim.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return claim;
    }

    public async Task<Claim> GetAsync(Guid claimId, ICurrentUser actor, CancellationToken ct = default) =>
        await LoadClaimInOrgAsync(claimId, actor, ct);

    public async Task<IReadOnlyList<Claim>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        return await _db.Claims.Where(c => c.PatientId == patient.Id)
            .OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
    }

    public async Task<ClaimTotalsDto> GetTotalsAsync(Guid claimId, ICurrentUser actor, CancellationToken ct = default)
    {
        var claim = await LoadClaimInOrgAsync(claimId, actor, ct);

        var totalCharge = await _db.Charges.Where(c => c.ClaimId == claim.Id).SumAsync(c => c.ChargeAmount, ct);

        var transactions = await _db.ClaimTransactions.Where(t => t.ClaimId == claim.Id).ToListAsync(ct);
        var payments = transactions.Where(t => t.Kind is ClaimTransactionKind.InsurancePayment or ClaimTransactionKind.PatientPayment).Sum(t => t.Amount);
        var refunds = transactions.Where(t => t.Kind == ClaimTransactionKind.Refund).Sum(t => t.Amount);
        var totalPaid = payments - refunds;
        var totalAdjusted = transactions.Where(t => t.Kind is ClaimTransactionKind.Adjustment or ClaimTransactionKind.WriteOff).Sum(t => t.Amount);

        return new ClaimTotalsDto(totalCharge, totalPaid, totalAdjusted, totalCharge - totalPaid - totalAdjusted);
    }

    private async Task<Claim> LoadClaimInOrgAsync(Guid claimId, ICurrentUser actor, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == claimId, ct)
            ?? throw new NotFoundException("Claim was not found.");
        if (claim.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Claim was not found.");
        }
        return claim;
    }
}
