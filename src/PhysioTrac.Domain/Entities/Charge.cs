using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>One billable line item — the unit that rolls up into a
/// <see cref="Claim"/> (insurance) or a <see cref="Superbill"/> (cash-pay),
/// never both. `RecommendedUnits` is always computed server-side from
/// documented timed minutes via the 8-minute-rule engine; it is never
/// client-supplied. A different `Units` value is always allowed — this only
/// assists billing staff — but requires `UnitsOverrideReason` once there is
/// a recommendation to diverge from.
///
/// Deliberately omits the original's `episode_of_care` FK — that entity
/// isn't ported yet.</summary>
public class Charge : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid? ClinicalNoteId { get; set; }
    public ClinicalNote? ClinicalNote { get; set; }

    /// <summary>The rendering clinician. Stored by id only — Domain doesn't
    /// reference Infrastructure's ApplicationUser.</summary>
    public Guid ProviderId { get; set; }

    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }

    public Guid? ClaimId { get; set; }
    public Claim? Claim { get; set; }

    public Guid? SuperbillId { get; set; }
    public Superbill? Superbill { get; set; }

    public DateOnly ServiceDate { get; set; }
    public string CptCode { get; set; } = string.Empty;

    /// <summary>Serialized JSON array of up to 4 two-character modifiers (e.g. GP, 59).</summary>
    public string ModifiersJson { get; set; } = "[]";

    public int Units { get; set; } = 1;
    public int? Minutes { get; set; }
    public int? RecommendedUnits { get; set; }
    public string? UnitsOverrideReason { get; set; }
    public decimal ChargeAmount { get; set; }
    public ChargeStatus Status { get; set; } = ChargeStatus.Draft;
    public Guid? CreatedById { get; set; }

    public ICollection<DiagnosisCode> DiagnosisCodes { get; set; } = new List<DiagnosisCode>();

    public int? UnitsDifference => RecommendedUnits is int recommended ? Units - recommended : null;
}
