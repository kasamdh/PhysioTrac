using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Billing;

/// <summary>Direct port of the `Claim` creation/validation rules in
/// `care/models.py` and the totals logic (`total_charge_amount`/
/// `total_paid`/`total_adjusted`/`balance`), computed from
/// <see cref="Charge"/>/<see cref="ClaimTransaction"/> rows rather than
/// stored. `billing_services.validate_claim`'s full pre-submission checklist
/// (timely filing, duplicate-claim detection, authorization, signed
/// documentation) is deferred — it needs `Authorization`, which isn't
/// ported yet.</summary>
public interface IClaimService
{
    /// <summary>Creates a claim from a set of Draft/Ready charges, snapshots
    /// the diagnosis code list from their union, and locks each charge to
    /// this claim (a charge already on a claim or superbill can't be reused).</summary>
    Task<Claim> CreateFromChargesAsync(CreateClaimRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<Claim> UpdateStatusAsync(Guid claimId, UpdateClaimStatusRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<Claim> GetAsync(Guid claimId, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<Claim>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);

    Task<ClaimTotalsDto> GetTotalsAsync(Guid claimId, ICurrentUser actor, CancellationToken ct = default);
}
