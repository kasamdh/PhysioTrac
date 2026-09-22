using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>Outcome-measure score and raw component data for deterministic trends.</summary>
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
}
