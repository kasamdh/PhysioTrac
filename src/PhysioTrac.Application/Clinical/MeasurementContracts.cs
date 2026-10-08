using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

/// <summary>One objective measurement (see ObjectiveMeasurement).</summary>
public record ObjectiveMeasurementDto(
    MeasurementCategory Category,
    string Item,
    string? Movement = null,
    BodySide? Side = null,
    string? Mode = null,
    decimal? NumericValue = null,
    string? TextValue = null,
    string? Unit = null,
    string? BodyRegion = null,
    string? EndFeel = null,
    bool? Painful = null,
    string? Compensation = null,
    string? AssistiveDevice = null,
    string? AssistanceLevel = null,
    string? Surface = null,
    string? Condition = null,
    string? Comment = null,
    Guid? Id = null);

public record MeasurementPointDto(DateOnly ServiceDate, decimal? NumericValue, string? TextValue);

/// <summary>One recorded measurement on a signed note (for trends).</summary>
public record PatientMeasurementDto(
    Guid NoteId, DateOnly ServiceDate, MeasurementCategory Category, string Item, string? Movement, BodySide? Side,
    string? Mode, string? Unit, decimal? NumericValue, string? TextValue);

/// <summary>The same measurement on the patient's signed notes: the first
/// one (baseline) and the last one before this note (previous).</summary>
public record MeasurementHistoryDto(
    MeasurementCategory Category, string Item, string? Movement, BodySide? Side, string? Mode, string? Unit,
    MeasurementPointDto Baseline, MeasurementPointDto? Previous);

public record SpecialTestResultDto(
    string TestName,
    SpecialTestOutcome Outcome = SpecialTestOutcome.NotTested,
    Guid? DefinitionId = null,
    ClinicalSpecialty Specialty = ClinicalSpecialty.General,
    string? BodyRegion = null,
    BodySide? Side = null,
    decimal? NumericValue = null,
    string? Unit = null,
    string? Interpretation = null,
    string? Comment = null,
    Guid? Id = null);

/// <summary>The last signed result of a test (same name and side).</summary>
public record SpecialTestHistoryDto(string TestName, BodySide? Side, DateOnly ServiceDate, SpecialTestOutcome Outcome, decimal? NumericValue, string? Unit);

public record SpecialTestDefinitionDto(
    Guid Id, string Code, string Name, ClinicalSpecialty Specialty, string? BodyRegion, string? Description,
    SpecialTestResultKind ResultKind, string? Unit, string? InterpretationGuide, string? ContraindicationWarning,
    bool IsActive, bool IsSystem, bool IsFavorite);

public record SaveSpecialTestDefinitionRequest(
    string Name, ClinicalSpecialty Specialty, SpecialTestResultKind ResultKind,
    string? BodyRegion = null, string? Description = null, string? Unit = null,
    string? InterpretationGuide = null, string? ContraindicationWarning = null);

/// <summary>What valid measurements and special-test results look like.</summary>
public static class MeasurementRules
{
    public const int MaxRows = 300;

    public static readonly IReadOnlySet<string> MmtGrades = new HashSet<string>
    {
        "0", "1", "2-", "2", "2+", "3-", "3", "3+", "4-", "4", "4+", "5",
    };

    public static IReadOnlyList<string> Validate(IReadOnlyList<ObjectiveMeasurementDto> rows)
    {
        var errors = new List<string>();
        if (rows.Count > MaxRows) errors.Add($"A note holds {MaxRows} measurements at most.");
        foreach (var (m, i) in rows.Select((m, i) => (m, i + 1)))
        {
            var where = $"Measurement {i} ({(string.IsNullOrWhiteSpace(m.Item) ? m.Category.ToString() : m.Item)})";
            if (string.IsNullOrWhiteSpace(m.Item)) errors.Add($"{where}: say what was measured.");
            if (Long(m.Item, 100) || Long(m.Movement, 100) || Long(m.Mode, 30) || Long(m.TextValue, 100) || Long(m.Unit, 20) ||
                Long(m.BodyRegion, 60) || Long(m.EndFeel, 40) || Long(m.Compensation, 300) || Long(m.AssistiveDevice, 100) ||
                Long(m.AssistanceLevel, 40) || Long(m.Surface, 60) || Long(m.Condition, 100) || Long(m.Comment, 1000))
                errors.Add($"{where}: a value is too long.");
            if (m.NumericValue is decimal v && (v < -100000 || v > 100000)) errors.Add($"{where}: the number is out of range.");
            if (m.Category == MeasurementCategory.RangeOfMotion && m.NumericValue is decimal deg && (deg < -90 || deg > 360))
                errors.Add($"{where}: range of motion must be -90 to 360 degrees.");
            if (m.Category == MeasurementCategory.Strength && string.Equals(m.Mode, "MMT", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(m.TextValue) && !MmtGrades.Contains(m.TextValue.Trim()))
                errors.Add($"{where}: \"{m.TextValue}\" is not a manual muscle test grade (0–5, with - or +).");
        }
        return errors;
    }

    public static IReadOnlyList<string> Validate(IReadOnlyList<SpecialTestResultDto> results)
    {
        var errors = new List<string>();
        if (results.Count > MaxRows) errors.Add($"A note holds {MaxRows} special tests at most.");
        foreach (var (t, i) in results.Select((t, i) => (t, i + 1)))
        {
            var where = $"Special test {i}";
            if (string.IsNullOrWhiteSpace(t.TestName)) errors.Add($"{where}: name the test.");
            if (Long(t.TestName, 150) || Long(t.BodyRegion, 60) || Long(t.Unit, 20) || Long(t.Interpretation, 500) || Long(t.Comment, 1000))
                errors.Add($"{where}: a value is too long.");
            if (t.NumericValue is decimal v && (v < -100000 || v > 100000)) errors.Add($"{where}: the number is out of range.");
        }
        return errors;
    }

    /// <summary>The identity of a measurement across visits.</summary>
    public static string Key(MeasurementCategory category, string item, string? movement, BodySide? side, string? mode, string? unit) =>
        string.Join('|', category, item.Trim().ToLowerInvariant(), movement?.Trim().ToLowerInvariant(), side,
            mode?.Trim().ToLowerInvariant(), unit?.Trim().ToLowerInvariant());

    private static bool Long(string? s, int max) => (s?.Length ?? 0) > max;
}
