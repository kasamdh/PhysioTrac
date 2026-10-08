using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>One structured treatment line item on a note (the "treatment
/// timer" — repeatable, billing-adjacent, and summed, unlike the free-text
/// <see cref="ClinicalNote.Interventions"/> field it sits alongside).</summary>
public class NoteIntervention : BaseEntity, INoteOwned
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

    // ---- flowsheet ----------------------------------------------------------

    /// <summary>The library entry it was picked from, if any.</summary>
    public Guid? LibraryItemId { get; set; }
    public InterventionLibraryItem? LibraryItem { get; set; }

    public string? CptCode { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public int? Sets { get; set; }
    public int? Repetitions { get; set; }
    public string? Resistance { get; set; }
    public string? Duration { get; set; }
    public string? Distance { get; set; }
    public string? Position { get; set; }
    public string? Equipment { get; set; }
    public string? AssistanceLevel { get; set; }
    public string? Cueing { get; set; }
    public string? Modification { get; set; }
    public int? PainBefore { get; set; }
    public int? PainAfter { get; set; }
    public InterventionStatus Status { get; set; } = InterventionStatus.Completed;
    public string? Comment { get; set; }

    /// <summary>Set when the entry was carried forward from an earlier note;
    /// the note can't be signed until the therapist marks it reviewed.</summary>
    public Guid? CarriedForwardFromNoteId { get; set; }
    public bool CarryForwardReviewed { get; set; }
}
