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

    /// <summary>Generates one Charge per distinct CPT code represented among
    /// a Signed note's NoteIntervention items, resolved via the
    /// organization's CptCodeMapping and priced from its fee schedule
    /// (location override, falling back to the org-wide ServicePrice row).
    /// A timed CPT code's units come from summing that code's own
    /// intervention minutes and running them through
    /// EightMinuteRuleCalculator using the organization's configured
    /// EightMinuteRuleVariant -- each code's units are computed
    /// independently; this does not implement cross-code remainder pooling,
    /// a further wrinkle real 8-minute-rule billing sometimes applies across
    /// multiple timed codes in the same visit. Throws if any categorized,
    /// timed intervention has no active CptCodeMapping for its category, or
    /// if this note already has generated charges (never silently
    /// duplicates).</summary>
    Task<IReadOnlyList<Charge>> GenerateFromNoteAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>The simpler, per-visit alternative to
    /// <see cref="GenerateFromNoteAsync"/>: bills a completed appointment as
    /// a single flat charge under its AppointmentType.DefaultCptCode, priced
    /// from AppointmentType.Price if set, otherwise the fee schedule. Throws
    /// if the appointment isn't Completed, its type has no DefaultCptCode
    /// configured, or a charge was already generated for this appointment.</summary>
    Task<Charge> GenerateFromAppointmentAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Location override first, falling back to the organization-
    /// wide ServicePrice row for the same CPT code; null if neither is
    /// configured. Exposed directly (not just used internally by the two
    /// generation methods above) so a billing screen can preview what a
    /// charge would be priced at before generating it.</summary>
    Task<decimal?> ResolveFeeScheduleAmountAsync(string cptCode, Guid? locationId, ICurrentUser actor, CancellationToken ct = default);
}
