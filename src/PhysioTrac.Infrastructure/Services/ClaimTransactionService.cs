using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Billing;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Direct port of `ClaimTransaction.clean()`.</summary>
public class ClaimTransactionService : IClaimTransactionService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;

    public ClaimTransactionService(PhysioTracDbContext db, ITenantAccessService tenantAccess)
    {
        _db = db;
        _tenantAccess = tenantAccess;
    }

    public async Task<ClaimTransaction> RecordAsync(RecordClaimTransactionRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Billing);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId && p.OrganizationId == organization.Id, ct)
            ?? throw new NotFoundException("Patient was not found.");

        Claim? claim = null;
        if (request.ClaimId is Guid claimId)
        {
            claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == claimId && c.OrganizationId == organization.Id, ct)
                ?? throw new NotFoundException("Claim was not found.");
            if (claim.PatientId != patient.Id)
            {
                throw new InvalidOperationException("Claim must belong to this patient.");
            }
        }

        if (request.Kind == ClaimTransactionKind.Transfer && request.TransferredToClaimId is null)
        {
            throw new InvalidOperationException("A transfer requires a destination claim.");
        }

        Claim? transferredToClaim = null;
        if (request.TransferredToClaimId is Guid transferId)
        {
            transferredToClaim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == transferId && c.OrganizationId == organization.Id, ct)
                ?? throw new NotFoundException("Destination claim was not found.");
            if (transferredToClaim.PatientId != patient.Id)
            {
                throw new InvalidOperationException("Destination claim must belong to this patient.");
            }
            if (transferredToClaim.Id == claim?.Id)
            {
                throw new InvalidOperationException("A transfer's destination must be a different claim.");
            }
        }

        if (request.Amount <= 0)
        {
            throw new InvalidOperationException("Amount must be greater than zero.");
        }

        var transaction = new ClaimTransaction
        {
            OrganizationId = organization.Id,
            PatientId = patient.Id,
            ClaimId = claim?.Id,
            TransferredToClaimId = transferredToClaim?.Id,
            Kind = request.Kind,
            Method = request.Method,
            Amount = request.Amount,
            PaymentDate = request.PaymentDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Reference = request.Reference,
            DenialCode = request.DenialCode,
            DenialReason = request.DenialReason,
            Notes = request.Notes,
            RecordedById = actor.UserId,
        };
        _db.ClaimTransactions.Add(transaction);
        await _db.SaveChangesAsync(ct);
        return transaction;
    }

    public async Task<IReadOnlyList<ClaimTransaction>> ListForClaimAsync(Guid claimId, ICurrentUser actor, CancellationToken ct = default)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == claimId && c.OrganizationId == organization.Id, ct)
            ?? throw new NotFoundException("Claim was not found.");
        return await _db.ClaimTransactions.Where(t => t.ClaimId == claim.Id)
            .OrderByDescending(t => t.PaymentDate).ThenByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
    }
}
