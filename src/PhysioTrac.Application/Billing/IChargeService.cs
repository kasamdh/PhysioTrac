using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Billing;

/// <summary>Direct port of the `Charge` capture rules in `care/models.py`
/// (`Charge.clean()`) — cross-org/cross-patient FK checks, the 4-modifier
/// limit, and the units-override-reason requirement once a recommendation
/// exists. Full claim validation (`billing_services.validate_claim`) is
/// deferred to a later module along with `Claim` itself.</summary>
public interface IChargeService
{
    /// <summary>Computes <c>RecommendedUnits</c> from <c>Minutes</c> via the
    /// 8-minute rule when <c>Minutes</c> is supplied, then creates the charge.
    /// Throws <see cref="Common.InvalidOperationException"/>-style domain
    /// errors for a CPT/modifier format violation or a missing override
    /// reason when <c>Units</c> diverges from the recommendation.</summary>
    Task<Charge> CreateAsync(CreateChargeRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<Charge> UpdateStatusAsync(Guid chargeId, UpdateChargeStatusRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<Charge> GetAsync(Guid chargeId, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<Charge>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default);
}
