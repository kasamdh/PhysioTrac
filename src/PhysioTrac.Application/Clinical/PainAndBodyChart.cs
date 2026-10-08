using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

/// <summary>A note's pain assessment. Ratings are on <see cref="Scale"/>.</summary>
public record PainAssessmentDto(
    PainScaleType Scale = PainScaleType.NumericRating,
    decimal? Current = null, decimal? Best = null, decimal? Worst = null,
    decimal? BeforeTreatment = null, decimal? AfterTreatment = null,
    string? Location = null,
    IReadOnlyList<string>? Qualities = null,
    PainFrequency? Frequency = null,
    string? Duration = null,
    PainIrritability? Irritability = null,
    string? AggravatingFactors = null, string? EasingFactors = null,
    string? DailyPattern = null, string? SleepImpact = null, string? FunctionalImpact = null);

/// <summary>One body-chart finding. X/Y are the point across the drawing (0-1).</summary>
public record BodyChartFindingDto(
    BodyView View, string Region, BodySide Side, decimal X, decimal Y, BodyFindingType FindingType,
    int? Severity = null, string? RadiatesTo = null, string? Annotation = null, string? Comment = null, Guid? Id = null);

/// <summary>The pain and body chart from the patient's last signed note before
/// this one, for comparison.</summary>
public record PreviousChartingDto(Guid NoteId, DateOnly ServiceDate, PainAssessmentDto? Pain, IReadOnlyList<BodyChartFindingDto> BodyChart);

public record PainHistoryPointDto(Guid NoteId, DateOnly ServiceDate, PainScaleType Scale, decimal? Current, decimal? Worst, decimal? BeforeTreatment, decimal? AfterTreatment);

/// <summary>What valid pain and body-chart data looks like.</summary>
public static class PainRules
{
    public static readonly IReadOnlyList<string> Qualities =
    [
        "Aching", "Sharp", "Dull", "Burning", "Throbbing", "Stabbing", "Shooting", "Cramping", "Tight", "Tingling",
    ];

    /// <summary>Body regions a finding can be placed in (keys shared with the drawing).</summary>
    public static readonly IReadOnlySet<string> Regions = new HashSet<string>
    {
        "head", "neck", "shoulder", "upperArm", "elbow", "forearm", "wristHand", "chest", "abdomen",
        "upperBack", "midBack", "lowBack", "pelvis", "buttock", "hip", "thigh", "knee", "lowerLeg", "calf", "ankleFoot", "other",
    };

    public const int MaxFindings = 60;

    public static (decimal Min, decimal Max) Range(PainScaleType scale) => scale switch
    {
        PainScaleType.VisualAnalog => (0, 100),
        PainScaleType.Verbal => (0, 3),
        _ => (0, 10),
    };

    public static IReadOnlyList<string> Validate(PainAssessmentDto pain)
    {
        var errors = new List<string>();
        var (min, max) = Range(pain.Scale);
        foreach (var (label, value) in new[]
        {
            ("Current pain", pain.Current), ("Best pain", pain.Best), ("Worst pain", pain.Worst),
            ("Pain before treatment", pain.BeforeTreatment), ("Pain after treatment", pain.AfterTreatment),
        })
        {
            if (value is decimal v && (v < min || v > max)) errors.Add($"{label} must be {min}–{max} on this scale.");
            if (value is decimal f && pain.Scale == PainScaleType.Faces && f % 2 != 0) errors.Add($"{label}: the faces scale uses 0, 2, 4, 6, 8 or 10.");
        }
        foreach (var q in pain.Qualities ?? [])
        {
            if (!Qualities.Contains(q)) errors.Add($"\"{q}\" is not a pain quality on the list.");
        }
        foreach (var (label, text, limit) in new[]
        {
            ("Location", pain.Location, 300), ("Duration", pain.Duration, 200), ("Aggravating factors", pain.AggravatingFactors, 1000),
            ("Easing factors", pain.EasingFactors, 1000), ("24-hour pattern", pain.DailyPattern, 1000),
            ("Sleep impact", pain.SleepImpact, 1000), ("Functional impact", pain.FunctionalImpact, 2000),
        })
        {
            if ((text?.Length ?? 0) > limit) errors.Add($"{label} is too long ({limit} characters at most).");
        }
        return errors;
    }

    public static IReadOnlyList<string> Validate(IReadOnlyList<BodyChartFindingDto> findings)
    {
        var errors = new List<string>();
        if (findings.Count > MaxFindings) errors.Add($"A body chart holds {MaxFindings} findings at most.");
        foreach (var (f, i) in findings.Select((f, i) => (f, i + 1)))
        {
            if (!Regions.Contains(f.Region ?? "")) errors.Add($"Finding {i}: unknown body region \"{f.Region}\".");
            if (f.X is < 0 or > 1 || f.Y is < 0 or > 1) errors.Add($"Finding {i}: the point must be on the drawing.");
            if (f.Severity is < 0 or > 10) errors.Add($"Finding {i}: severity must be 0–10.");
            if ((f.Annotation?.Length ?? 0) > 200 || (f.RadiatesTo?.Length ?? 0) > 200 || (f.Comment?.Length ?? 0) > 1000)
                errors.Add($"Finding {i}: the text is too long.");
        }
        return errors;
    }

    public static bool IsEmpty(PainAssessmentDto p) =>
        p.Current is null && p.Best is null && p.Worst is null && p.BeforeTreatment is null && p.AfterTreatment is null &&
        string.IsNullOrWhiteSpace(p.Location) && (p.Qualities?.Count ?? 0) == 0 && p.Frequency is null &&
        string.IsNullOrWhiteSpace(p.Duration) && p.Irritability is null && string.IsNullOrWhiteSpace(p.AggravatingFactors) &&
        string.IsNullOrWhiteSpace(p.EasingFactors) && string.IsNullOrWhiteSpace(p.DailyPattern) &&
        string.IsNullOrWhiteSpace(p.SleepImpact) && string.IsNullOrWhiteSpace(p.FunctionalImpact);
}

/// <summary>The body chart recorded on one signed note.</summary>
public record BodyChartHistoryDto(Guid NoteId, DateOnly ServiceDate, NoteType NoteType, IReadOnlyList<BodyChartFindingDto> Findings);
