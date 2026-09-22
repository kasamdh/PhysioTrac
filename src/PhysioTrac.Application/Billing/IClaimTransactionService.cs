using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Billing;

/// <summary>Direct port of `ClaimTransaction.clean()` — a transfer requires
/// a destination claim, the destination must belong to the same patient and
/// be a different claim than the source, and the amount must be positive.</summary>
public interface IClaimTransactionService
{
    Task<ClaimTransaction> RecordAsync(RecordClaimTransactionRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<ClaimTransaction>> ListForClaimAsync(Guid claimId, ICurrentUser actor, CancellationToken ct = default);
}
