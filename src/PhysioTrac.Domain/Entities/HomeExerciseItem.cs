using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>One exercise line item on a HomeExerciseProgram -- sets/reps/
/// hold time are all optional since not every exercise (e.g. a timed plank,
/// a walk) is described the same way.</summary>
public class HomeExerciseItem : BaseEntity
{
    public Guid ProgramId { get; set; }
    public HomeExerciseProgram? Program { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? Sets { get; set; }
    public int? Reps { get; set; }
    public int? HoldSeconds { get; set; }
    public int? FrequencyPerDay { get; set; }
    public string? Notes { get; set; }

    /// <summary>Link to a demonstration image or video (YouTube, a stored
    /// asset, etc.) -- stored as a plain URL, not an uploaded file; actual
    /// media hosting/upload is a frontend/CDN concern outside this API.</summary>
    public string? MediaUrl { get; set; }

    public int Order { get; set; }
}
