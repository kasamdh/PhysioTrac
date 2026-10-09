using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>A note's structured pain assessment (at most one per note).
/// Ratings are on <see cref="Scale"/>; qualities are a "|"-separated list of
/// the known quality words (PainRules.Qualities).</summary>
public class PainAssessment : BaseEntity, INoteOwned
{
    public Guid NoteId { get; set; }
    public ClinicalNote? Note { get; set; }

    public Guid PatientId { get; set; }

    public PainScaleType Scale { get; set; } = PainScaleType.NumericRating;
    public decimal? Current { get; set; }
    public decimal? Best { get; set; }
    public decimal? Worst { get; set; }
    public decimal? BeforeTreatment { get; set; }
    public decimal? AfterTreatment { get; set; }

    public string? Location { get; set; }
    public string? Qualities { get; set; }
    public PainFrequency? Frequency { get; set; }
    public string? Duration { get; set; }
    public PainIrritability? Irritability { get; set; }
    public string? AggravatingFactors { get; set; }
    public string? EasingFactors { get; set; }
    public string? DailyPattern { get; set; }
    public string? SleepImpact { get; set; }
    public string? FunctionalImpact { get; set; }
}

/// <summary>One finding marked on a note's body chart: where (view, region,
/// side and the exact point, 0-1 across the drawing), what, how severe,
/// and the therapist's notes.</summary>
public class BodyChartFinding : BaseEntity, INoteOwned
{
    public Guid NoteId { get; set; }
    public ClinicalNote? Note { get; set; }

    public Guid PatientId { get; set; }

    public BodyView View { get; set; }
    public string Region { get; set; } = string.Empty;
    public BodySide Side { get; set; }
    public decimal X { get; set; }
    public decimal Y { get; set; }

    public BodyFindingType FindingType { get; set; }
    public int? Severity { get; set; }
    public string? RadiatesTo { get; set; }
    public string? Annotation { get; set; }
    public string? Comment { get; set; }
    public int Order { get; set; }
}
