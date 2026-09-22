using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>One structured treatment line item on a note (the "treatment
/// timer" — repeatable, billing-adjacent, and summed, unlike the free-text
/// <see cref="ClinicalNote.Interventions"/> field it sits alongside).</summary>
public class NoteIntervention : BaseEntity
{
    public Guid NoteId { get; set; }
    public ClinicalNote? Note { get; set; }

    public string Description { get; set; } = string.Empty;
    public string? BodyRegion { get; set; }

    /// <summary>Null means "not categorized" — deliberately excluded from
    /// CPT-code suggestions rather than guessed from free text.</summary>
    public InterventionCategory? Category { get; set; }

    public int Minutes { get; set; }
    public int? Units { get; set; }
    public bool IsTimed { get; set; } = true;
    public string? PatientResponse { get; set; }
    public int Order { get; set; }
}
