using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>One recorded allergy/adverse reaction on a patient's chart.
/// Never hard-deleted once recorded -- a mistakenly-entered allergy is
/// marked inactive (IsActive = false with a reason), not removed, since
/// "this patient does NOT have a penicillin allergy" is exactly the kind of
/// correction that needs its own trail rather than silently vanishing.</summary>
public class PatientAllergy : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public string Allergen { get; set; } = string.Empty;
    public string? Reaction { get; set; }
    public AllergySeverity Severity { get; set; } = AllergySeverity.Unknown;
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
    public Guid RecordedById { get; set; }
}
