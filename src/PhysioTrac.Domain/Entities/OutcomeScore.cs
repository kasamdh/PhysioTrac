using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>An outcome-measure score with its raw item responses (JSON list of
/// OutcomeItemResponseDto, or "{}" when only a total was entered).</summary>
public class OutcomeScore : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid? NoteId { get; set; }
    public ClinicalNote? Note { get; set; }

    public Guid RecordedById { get; set; }

    public OutcomeMeasure Measure { get; set; }
    public DateOnly MeasuredOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public decimal Score { get; set; }
    public decimal? MaximumScore { get; set; }
    public string ItemResponsesJson { get; set; } = "{}";
    public string? Notes { get; set; }

    /// <summary>The interpretation shown when the score was recorded.</summary>
    public string? Interpretation { get; set; }
}
