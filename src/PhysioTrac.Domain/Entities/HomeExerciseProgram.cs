using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>A patient's assigned home exercise program -- the patient-facing
/// counterpart of <see cref="ClinicalNote.Interventions"/>/<see cref="NoteIntervention"/>
/// (what was actually done in clinic) rather than a duplicate of it. A
/// patient can have more than one program over time as their plan of care
/// changes; old ones are discontinued, never deleted, so history stays
/// intact.</summary>
public class HomeExerciseProgram : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid CreatedById { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? GeneralInstructions { get; set; }
    public HomeExerciseProgramStatus Status { get; set; } = HomeExerciseProgramStatus.Active;

    public DateTimeOffset? DiscontinuedAt { get; set; }
    public Guid? DiscontinuedById { get; set; }

    public ICollection<HomeExerciseItem> Items { get; set; } = new List<HomeExerciseItem>();

    public bool IsActive => Status == HomeExerciseProgramStatus.Active;
}
