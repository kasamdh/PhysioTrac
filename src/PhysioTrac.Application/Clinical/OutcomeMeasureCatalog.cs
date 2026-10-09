using System.Globalization;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

/// <summary>How a measure's item responses become its score.</summary>
public enum OutcomeScoringMethod
{
    /// <summary>Sum of every item (all items required unless MinAnswered says otherwise).</summary>
    Sum,
    /// <summary>Sum / (item maximum x items answered) x 100 -- ODI.</summary>
    PercentOfAnswered,
    /// <summary>Sum scaled up to the full item count when items are skipped -- NDI.</summary>
    ProratedSum,
    /// <summary>((sum / items answered) - 1) x 25 -- QuickDASH.</summary>
    QuickDash,
    /// <summary>Mean of the items answered -- PSFS, ABC.</summary>
    Mean,
    /// <summary>Mean of the timed trials entered, in seconds -- TUG, 5xSTS.</summary>
    TimedMean,
}

public record OutcomeItemDto(string Key, string Label);
public record OutcomeOptionDto(decimal Value, string Label);

/// <summary>An interpretation band: applies from <see cref="Min"/> up to the next band's Min.</summary>
public record OutcomeBandDto(decimal Min, string Label);

/// <summary>Everything needed to administer, score and interpret a measure.
/// Items carry short labels only: administer the official form and enter
/// each item's score here.</summary>
public record OutcomeMeasureDefinitionDto(
    OutcomeMeasure Measure, string Code, string Abbreviation, string Name, string Domain, string Description,
    OutcomeScoringMethod Scoring, IReadOnlyList<OutcomeItemDto> Items, decimal ItemMin, decimal ItemMax, decimal ItemStep,
    IReadOnlyList<OutcomeOptionDto> Options, int MinAnswered, bool ItemsNamedByPatient,
    decimal ScoreMin, decimal? ScoreMax, string Unit, bool HigherIsBetter, int Decimals,
    decimal? MeaningfulChange, string? MeaningfulChangeNote, IReadOnlyList<OutcomeBandDto> Bands, string? Reference);

/// <summary>One item's response. <see cref="Label"/> is the activity the
/// patient named (PSFS) or null.</summary>
public record OutcomeItemResponseDto(string Key, decimal? Value, string? Label = null);

public record OutcomeScoreResult(decimal Score, decimal? MaximumScore, string Interpretation, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>The outcome measures PhysioTrac can score. To add a measure:
/// append an <see cref="OutcomeMeasure"/> value and one definition below
/// (the API, the React entry form, trend chart and comparison pick it up
/// from GET /outcomes/measures), then add its scoring test.</summary>
public static class OutcomeMeasureCatalog
{
    private static OutcomeItemDto[] Items(params string[] labels) =>
        labels.Select((l, i) => new OutcomeItemDto($"i{i + 1}", l)).ToArray();

    private static OutcomeOptionDto[] Range(int from, int to, Func<int, string> label) =>
        Enumerable.Range(from, to - from + 1).Select(v => new OutcomeOptionDto(v, label(v))).ToArray();

    private static readonly OutcomeItemDto[] Trials =
        [new("trial1", "Trial 1 (seconds)"), new("trial2", "Trial 2 (seconds)"), new("trial3", "Trial 3 (seconds)")];

    public static IReadOnlyList<OutcomeMeasureDefinitionDto> All { get; } =
    [
        new(OutcomeMeasure.Lefs, "lefs", "LEFS", "Lower Extremity Functional Scale", "Lower extremity function",
            "20 activities rated from 0 (extreme difficulty or unable) to 4 (no difficulty).",
            OutcomeScoringMethod.Sum,
            Items("Usual work, housework or school activities", "Usual hobbies, recreational or sporting activities",
                "Getting into or out of the bath", "Walking between rooms", "Putting on shoes or socks", "Squatting",
                "Lifting an object, like a bag of groceries, from the floor", "Light activities around the home",
                "Heavy activities around the home", "Getting into or out of a car", "Walking 2 blocks", "Walking a mile",
                "Going up or down 10 stairs (about 1 flight)", "Standing for 1 hour", "Sitting for 1 hour",
                "Running on even ground", "Running on uneven ground", "Making sharp turns while running fast", "Hopping",
                "Rolling over in bed"),
            0, 4, 1,
            [new(0, "0 — Extreme difficulty or unable"), new(1, "1 — Quite a bit of difficulty"), new(2, "2 — Moderate difficulty"),
                new(3, "3 — A little bit of difficulty"), new(4, "4 — No difficulty")],
            20, false, 0, 80, "points", true, 0, 9, "Minimal clinically important difference ≈ 9 points.", [], null),

        new(OutcomeMeasure.Odi, "odi", "ODI", "Oswestry Disability Index", "Low back",
            "10 sections scored 0–5; reported as a percentage of the maximum for the sections answered.",
            OutcomeScoringMethod.PercentOfAnswered,
            Items("Pain intensity", "Personal care", "Lifting", "Walking", "Sitting", "Standing", "Sleeping", "Sex life (if applicable)",
                "Social life", "Travelling"),
            0, 5, 1, Range(0, 5, v => v == 0 ? "0 — least limitation" : v == 5 ? "5 — most limitation" : v.ToString()),
            9, false, 0, 100, "%", false, 0, 10, "Minimal clinically important difference ≈ 10 percentage points.",
            [new(0, "Minimal disability (0–20%)"), new(21, "Moderate disability (21–40%)"), new(41, "Severe disability (41–60%)"),
                new(61, "Very severe disability (61–80%)"), new(81, "Bed-bound or symptoms possibly exaggerated (81–100%)")],
            "Bands from the ODI scoring guide. One unanswered section is allowed."),

        new(OutcomeMeasure.Ndi, "ndi", "NDI", "Neck Disability Index", "Neck",
            "10 sections scored 0–5; total out of 50 (scaled up when one section is unanswered).",
            OutcomeScoringMethod.ProratedSum,
            Items("Pain intensity", "Personal care", "Lifting", "Reading", "Headaches", "Concentration", "Work", "Driving", "Sleeping",
                "Recreation"),
            0, 5, 1, Range(0, 5, v => v == 0 ? "0 — least limitation" : v == 5 ? "5 — most limitation" : v.ToString()),
            9, false, 0, 50, "points", false, 0, 7.5m, "Minimal clinically important difference ≈ 7.5 points (out of 50).",
            [new(0, "No disability (0–4)"), new(5, "Mild disability (5–14)"), new(15, "Moderate disability (15–24)"),
                new(25, "Severe disability (25–34)"), new(35, "Complete disability (35–50)")],
            "Bands from Vernon's scoring guide. One unanswered section is allowed."),

        new(OutcomeMeasure.QuickDash, "quickdash", "QuickDASH", "QuickDASH (Disabilities of the Arm, Shoulder and Hand)",
            "Upper extremity function",
            "11 items scored 1–5; score = (mean of answered items − 1) × 25, from 0 (no disability) to 100.",
            OutcomeScoringMethod.QuickDash,
            Items("Open a tight or new jar", "Do heavy household chores", "Carry a shopping bag or briefcase", "Wash your back",
                "Use a knife to cut food", "Recreation with force or impact through the arm, shoulder or hand",
                "Interference with normal social activities", "Limitation in work or other daily activities",
                "Arm, shoulder or hand pain", "Tingling (pins and needles)", "Difficulty sleeping because of pain"),
            1, 5, 1, Range(1, 5, v => v == 1 ? "1 — none / no difficulty" : v == 5 ? "5 — extreme / unable" : v.ToString()),
            10, false, 0, 100, "points", false, 1, 8, "Minimal clinically important difference reported at about 8–16 points.", [],
            "At least 10 of the 11 items must be answered."),

        new(OutcomeMeasure.Psfs, "psfs", "PSFS", "Patient-Specific Functional Scale", "Patient-specific function",
            "The patient names up to 5 activities they find hard and rates each from 0 (unable) to 10 (able at prior level).",
            OutcomeScoringMethod.Mean,
            [new("activity1", "Activity 1"), new("activity2", "Activity 2"), new("activity3", "Activity 3"),
                new("activity4", "Activity 4"), new("activity5", "Activity 5")],
            0, 10, 1, Range(0, 10, v => v == 0 ? "0 — unable" : v == 10 ? "10 — able at prior level" : v.ToString()),
            1, true, 0, 10, "points", true, 1, 2, "Minimal detectable change ≈ 2 points for the average score (3 for a single activity).",
            [], "Rate the same activities at each visit."),

        new(OutcomeMeasure.Berg, "berg", "BBS", "Berg Balance Scale", "Balance",
            "14 tasks scored 0–4; total out of 56.",
            OutcomeScoringMethod.Sum,
            Items("Sitting to standing", "Standing unsupported", "Sitting unsupported", "Standing to sitting", "Transfers",
                "Standing with eyes closed", "Standing with feet together", "Reaching forward with outstretched arm",
                "Retrieving an object from the floor", "Turning to look behind", "Turning 360 degrees",
                "Placing alternate foot on a stool", "Standing with one foot in front", "Standing on one foot"),
            0, 4, 1, Range(0, 4, v => v == 0 ? "0 — unable / needs most help" : v == 4 ? "4 — independent and safe" : v.ToString()),
            14, false, 0, 56, "points", true, 0, 5, "Minimal detectable change ≈ 5 points in older adults.",
            [new(0, "High fall risk (0–20)"), new(21, "Medium fall risk (21–40)"), new(41, "Low fall risk (41–56)")],
            "Scores of 45 or less are commonly associated with increased fall risk in older adults."),

        new(OutcomeMeasure.Tug, "tug", "TUG", "Timed Up and Go", "Mobility and fall risk",
            "Rise from a chair, walk 3 m, turn, walk back and sit, at a comfortable pace. Enter 1–3 trials; the mean is scored.",
            OutcomeScoringMethod.TimedMean, Trials, 0.1m, 300, 0.01m, [], 1, false, 0, null, "seconds", false, 1, null, null,
            [new(0, "Typical for community-dwelling older adults (under 10 s)"), new(10, "Slower than typical (10–13.4 s)"),
                new(13.5m, "Increased fall risk (13.5 s or more, community-dwelling older adults)")],
            "Record any assistive device in the notes; use the same device for comparison."),

        new(OutcomeMeasure.FiveTimesSitToStand, "5xsts", "5xSTS", "Five Times Sit-to-Stand", "Lower extremity strength and balance",
            "Time to stand up fully and sit down 5 times as quickly as possible with arms folded. Enter 1–3 trials; the mean is scored.",
            OutcomeScoringMethod.TimedMean, Trials, 0.1m, 300, 0.01m, [], 1, false, 0, null, "seconds", false, 1,
            2.3m, "Minimal clinically important difference ≈ 2.3 seconds (vestibular disorders).",
            [new(0, "Typical (under 12 s)"), new(12, "Slower than typical (12–14.9 s)"),
                new(15, "Increased fall risk (15 s or more, older adults)")],
            "Reference values vary with age; compare against age norms."),

        new(OutcomeMeasure.Abc, "abc", "ABC", "Activities-specific Balance Confidence Scale", "Balance confidence",
            "16 activities rated for confidence from 0% (none) to 100% (complete); score = mean of the 16 ratings.",
            OutcomeScoringMethod.Mean,
            Items("Walk around the house", "Walk up or down stairs", "Bend over and pick up a slipper from the floor",
                "Reach for a small can on a shelf at eye level", "Stand on tiptoes and reach above the head",
                "Stand on a chair and reach for something", "Sweep the floor", "Walk outside to a nearby car",
                "Get into or out of a car", "Walk across a parking lot", "Walk up or down a ramp", "Walk in a crowded mall",
                "Get bumped into while walking in a crowd", "Escalator holding the railing", "Escalator without holding the railing",
                "Walk outside on icy sidewalks"),
            0, 100, 1, [], 16, false, 0, 100, "%", true, 1, null, null,
            [new(0, "Low level of physical functioning (under 50%)"), new(50, "Moderate level of functioning (50–79%)"),
                new(80, "High level of functioning (80% or more)")],
            "Scores below 67% are commonly associated with increased fall risk in older adults."),

        new(OutcomeMeasure.Fga, "fga", "FGA", "Functional Gait Assessment", "Gait and dynamic balance",
            "10 walking tasks scored 0 (severe impairment) to 3 (normal); total out of 30.",
            OutcomeScoringMethod.Sum,
            Items("Gait on a level surface", "Change in gait speed", "Gait with horizontal head turns", "Gait with vertical head turns",
                "Gait and pivot turn", "Step over obstacle", "Gait with narrow base of support", "Gait with eyes closed",
                "Ambulating backwards", "Steps"),
            0, 3, 1,
            [new(0, "0 — Severe impairment"), new(1, "1 — Moderate impairment"), new(2, "2 — Mild impairment"), new(3, "3 — Normal")],
            10, false, 0, 30, "points", true, 0, 4, "Minimal clinically important difference ≈ 4 points (vestibular disorders).",
            [new(0, "Increased fall risk (22 or less, older adults)"), new(23, "Lower fall risk (23–30)")],
            null),
    ];

    private static readonly Dictionary<OutcomeMeasure, OutcomeMeasureDefinitionDto> ByMeasure = All.ToDictionary(d => d.Measure);

    public static OutcomeMeasureDefinitionDto Get(OutcomeMeasure measure) =>
        ByMeasure.TryGetValue(measure, out var d) ? d : throw new ArgumentOutOfRangeException(nameof(measure), "Unknown outcome measure.");

    public static bool IsKnown(OutcomeMeasure measure) => ByMeasure.ContainsKey(measure);

    /// <summary>Scores a set of item responses: validates every response,
    /// checks enough items are answered, applies the measure's scoring
    /// method and interprets the result.</summary>
    public static OutcomeScoreResult Score(OutcomeMeasure measure, IReadOnlyList<OutcomeItemResponseDto> responses)
    {
        var d = Get(measure);
        var errors = new List<string>();
        var keys = d.Items.Select(i => i.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var r in responses)
        {
            if (!keys.Contains(r.Key)) errors.Add($"{d.Abbreviation}: \"{r.Key}\" is not an item of this measure.");
        }
        foreach (var dup in responses.GroupBy(r => r.Key).Where(g => g.Count() > 1))
            errors.Add($"{d.Abbreviation}: item \"{dup.Key}\" is answered more than once.");

        var answered = new List<decimal>();
        foreach (var item in d.Items)
        {
            var r = responses.FirstOrDefault(x => x.Key == item.Key);
            if (r?.Value is not decimal v) continue;
            var label = d.ItemsNamedByPatient ? r.Label?.Trim() : item.Label;
            if (v < d.ItemMin || v > d.ItemMax)
                errors.Add($"{d.Abbreviation} — {item.Label}: enter {d.ItemMin}–{d.ItemMax}.");
            else if (d.ItemStep > 0 && v % d.ItemStep != 0)
                errors.Add($"{d.Abbreviation} — {item.Label}: use steps of {d.ItemStep}.");
            else if (d.ItemsNamedByPatient && string.IsNullOrWhiteSpace(label))
                errors.Add($"{d.Abbreviation} — {item.Label}: name the activity.");
            answered.Add(v);
        }
        if (answered.Count < d.MinAnswered)
        {
            errors.Add(d.MinAnswered == d.Items.Count
                ? $"{d.Abbreviation}: answer all {d.Items.Count} items ({answered.Count} answered)."
                : $"{d.Abbreviation}: answer at least {d.MinAnswered} of {d.Items.Count} items ({answered.Count} answered).");
        }
        if (errors.Count > 0) return new OutcomeScoreResult(0, d.ScoreMax, string.Empty, errors);

        var sum = answered.Sum();
        var raw = d.Scoring switch
        {
            OutcomeScoringMethod.Sum => sum,
            OutcomeScoringMethod.PercentOfAnswered => sum / (d.ItemMax * answered.Count) * 100,
            OutcomeScoringMethod.ProratedSum => sum * d.Items.Count / answered.Count,
            OutcomeScoringMethod.QuickDash => (sum / answered.Count - 1) * 25,
            OutcomeScoringMethod.Mean or OutcomeScoringMethod.TimedMean => sum / answered.Count,
            _ => throw new InvalidOperationException("Unknown scoring method."),
        };
        var score = Math.Round(raw, d.Decimals, MidpointRounding.AwayFromZero);
        return new OutcomeScoreResult(score, d.ScoreMax, Interpret(measure, score), []);
    }

    /// <summary>Range check for a total entered without item responses.</summary>
    public static IReadOnlyList<string> ValidateTotal(OutcomeMeasure measure, decimal score)
    {
        var d = Get(measure);
        if (score < d.ScoreMin || (d.ScoreMax is decimal max && score > max) || (d.ScoreMax is null && score <= 0))
            return [d.ScoreMax is decimal m ? $"{d.Abbreviation}: the score must be {d.ScoreMin}–{m}." : $"{d.Abbreviation}: the time must be more than 0."];
        return [];
    }

    /// <summary>The interpretation for a score: its band, or (for measures
    /// without published bands) where it sits on the scale.</summary>
    public static string Interpret(OutcomeMeasure measure, decimal score)
    {
        var d = Get(measure);
        var band = d.Bands.Where(b => score >= b.Min).OrderBy(b => b.Min).LastOrDefault();
        if (band is not null) return band.Label;
        if (d.ScoreMax is decimal max && max > d.ScoreMin)
        {
            var pct = Math.Round((score - d.ScoreMin) / (max - d.ScoreMin) * 100);
            return d.HigherIsBetter
                ? $"{pct}% of maximum function (higher is better)"
                : $"{pct}% of maximum disability (lower is better)";
        }
        return d.HigherIsBetter ? "Higher is better" : "Lower is better";
    }

    /// <summary>A score as text: "52/80", "42/56", "12.1 s".</summary>
    public static string Format(OutcomeMeasure measure, decimal score)
    {
        var d = Get(measure);
        var n = score.ToString("0.##", CultureInfo.InvariantCulture);
        return d.ScoreMax is decimal max
            ? $"{n}/{max.ToString("0.##", CultureInfo.InvariantCulture)}{(d.Unit == "%" ? "%" : "")}"
            : $"{n} {(d.Unit == "seconds" ? "s" : d.Unit)}";
    }

    /// <summary>A change between two scores: amount, and whether it is better
    /// (which way depends on the measure) and reaches the meaningful-change
    /// value -- e.g. "+12 points, meaningful improvement".</summary>
    public static string DescribeChange(OutcomeMeasure measure, decimal from, decimal to)
    {
        var d = Get(measure);
        var delta = to - from;
        if (delta == 0) return "no change";
        var unit = d.Unit == "%" ? "points" : d.Unit;
        var amount = $"{(delta > 0 ? "+" : "")}{delta.ToString("0.##", CultureInfo.InvariantCulture)} {unit}";
        var better = d.HigherIsBetter ? delta > 0 : delta < 0;
        var meaningful = d.MeaningfulChange is decimal m && Math.Abs(delta) >= m;
        return $"{amount}, {(better ? meaningful ? "meaningful improvement" : "improved" : meaningful ? "meaningful decline" : "worse")}";
    }
}
