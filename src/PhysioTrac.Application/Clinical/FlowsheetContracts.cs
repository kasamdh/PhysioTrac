using PhysioTrac.Application.Billing;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

/// <summary>One intervention / exercise on a note's flowsheet.</summary>
public record FlowsheetEntryDto(
    string Description,
    InterventionCategory? Category = null,
    string? CptCode = null,
    bool IsTimed = true,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    int Minutes = 0,
    int? Units = null,
    int? Sets = null,
    int? Repetitions = null,
    string? Resistance = null,
    string? Duration = null,
    string? Distance = null,
    string? Position = null,
    string? Equipment = null,
    string? AssistanceLevel = null,
    string? Cueing = null,
    string? Modification = null,
    string? PatientResponse = null,
    int? PainBefore = null,
    int? PainAfter = null,
    InterventionStatus Status = InterventionStatus.Completed,
    string? Comment = null,
    string? BodyRegion = null,
    Guid? LibraryItemId = null,
    Guid? CarriedForwardFromNoteId = null,
    bool CarryForwardReviewed = false,
    Guid? Id = null);

public record FlowsheetWarningDto(string Code, string Message, int? Entry = null);

/// <summary>Totals and billing hints for a flowsheet. Advisory only: nothing
/// here is billed, submitted, or blocks signing.</summary>
public record FlowsheetSummaryDto(
    int TimedMinutes, int UntimedServices, int EstimatedTimedUnits, int? EnteredTimedUnits, string RuleVariant,
    IReadOnlyList<FlowsheetWarningDto> Warnings);

/// <summary>The flowsheet of the patient's last signed note before this one.</summary>
public record PreviousFlowsheetDto(Guid NoteId, DateOnly ServiceDate, IReadOnlyList<FlowsheetEntryDto> Entries);

public record InterventionLibraryItemDto(
    Guid Id, string Code, string Name, InterventionCategory Category, string? CptCode, bool IsTimed, string? BodyRegion,
    string? Description, int? DefaultSets, int? DefaultRepetitions, string? DefaultResistance, string? DefaultDuration,
    string? DefaultEquipment, string? DefaultPosition, bool IsActive, bool IsSystem, bool IsFavorite);

public record SaveInterventionLibraryItemRequest(
    string Name, InterventionCategory Category, bool IsTimed, string? CptCode = null, string? BodyRegion = null,
    string? Description = null, int? DefaultSets = null, int? DefaultRepetitions = null, string? DefaultResistance = null,
    string? DefaultDuration = null, string? DefaultEquipment = null, string? DefaultPosition = null);

public record InterventionGroupItemDto(
    string Name, InterventionCategory Category, bool IsTimed = true, string? CptCode = null, int? Sets = null, int? Repetitions = null,
    string? Resistance = null, string? Duration = null, string? Equipment = null, string? Position = null, Guid? LibraryItemId = null);

public record InterventionGroupDto(
    Guid Id, string Name, string? Description, bool IsShared, bool IsMine, bool IsFavorite, IReadOnlyList<InterventionGroupItemDto> Items);

/// <summary>Shared = visible to the whole clinic (administrators/directors only).</summary>
public record SaveInterventionGroupRequest(string Name, IReadOnlyList<InterventionGroupItemDto> Items, string? Description = null, bool IsShared = false);

/// <summary>Flowsheet validation and the advisory billing/consistency checks.</summary>
public static class FlowsheetRules
{
    public const int MaxEntries = 100;

    public static IReadOnlyList<string> Validate(IReadOnlyList<FlowsheetEntryDto> entries)
    {
        var errors = new List<string>();
        if (entries.Count > MaxEntries) errors.Add($"A flowsheet holds {MaxEntries} entries at most.");
        foreach (var (e, i) in entries.Select((e, i) => (e, i + 1)))
        {
            var where = $"Flowsheet entry {i} ({(string.IsNullOrWhiteSpace(e.Description) ? "unnamed" : e.Description)})";
            if (string.IsNullOrWhiteSpace(e.Description)) errors.Add($"{where}: name the intervention.");
            if (e.Minutes is < 0 or > 480) errors.Add($"{where}: minutes must be 0–480.");
            if (e.Units is < 0 or > 32) errors.Add($"{where}: units must be 0–32.");
            if (e.Sets is < 0 or > 100 || e.Repetitions is < 0 or > 1000) errors.Add($"{where}: sets or repetitions are out of range.");
            if (e.PainBefore is < 0 or > 10 || e.PainAfter is < 0 or > 10) errors.Add($"{where}: pain must be 0–10.");
            if (Long(e.Description, 300) || Long(e.CptCode, 10) || Long(e.Resistance, 60) || Long(e.Duration, 60) || Long(e.Distance, 60) ||
                Long(e.Position, 60) || Long(e.Equipment, 100) || Long(e.AssistanceLevel, 40) || Long(e.Cueing, 60) ||
                Long(e.Modification, 200) || Long(e.PatientResponse, 1000) || Long(e.Comment, 1000) || Long(e.BodyRegion, 60))
                errors.Add($"{where}: a value is too long.");
        }
        return errors;
    }

    private static bool Long(string? s, int max) => (s?.Length ?? 0) > max;

    /// <summary>Entries that count toward this visit's treatment (not held or discontinued).</summary>
    private static bool Performed(FlowsheetEntryDto e) => e.Status is InterventionStatus.Completed or InterventionStatus.Modified;

    public static FlowsheetSummaryDto Analyze(IReadOnlyList<FlowsheetEntryDto> entries, EightMinuteRuleVariant variant)
    {
        var performed = entries.Select((e, i) => (e, i)).Where(x => Performed(x.e)).ToList();
        var timed = performed.Where(x => x.e.IsTimed).ToList();
        var timedMinutes = timed.Sum(x => x.e.Minutes);
        var estimated = EightMinuteRuleCalculator.ComputeUnits(timedMinutes, variant);
        int? entered = timed.Any(x => x.e.Units is not null) ? timed.Sum(x => x.e.Units ?? 0) : null;
        var warnings = new List<FlowsheetWarningDto>();

        foreach (var (e, i) in performed)
        {
            var n = i + 1;
            if (string.IsNullOrWhiteSpace(e.PatientResponse))
                warnings.Add(new("missing_response", $"Entry {n} ({e.Description}): add the patient's response.", n));
            if (e.IsTimed && e.Minutes == 0)
                warnings.Add(new("no_minutes", $"Entry {n} ({e.Description}): a timed service has no minutes.", n));
            if (e.StartTime is TimeOnly s && e.EndTime is TimeOnly end)
            {
                if (end <= s) warnings.Add(new("end_before_start", $"Entry {n} ({e.Description}): the end time is not after the start time.", n));
                else if (Math.Abs((int)(end - s).TotalMinutes - e.Minutes) > 1)
                    warnings.Add(new("minutes_mismatch", $"Entry {n} ({e.Description}): {e.Minutes} min recorded but {(int)(end - s).TotalMinutes} min between start and end.", n));
            }
            if (!e.IsTimed && e.Units is > 1)
                warnings.Add(new("untimed_units", $"Entry {n} ({e.Description}): an untimed service is usually 1 unit.", n));
        }

        var withTimes = timed.Where(x => x.e.StartTime is not null && x.e.EndTime is not null && x.e.EndTime > x.e.StartTime).ToList();
        for (var a = 0; a < withTimes.Count; a++)
        {
            for (var b = a + 1; b < withTimes.Count; b++)
            {
                var (x, y) = (withTimes[a], withTimes[b]);
                if (x.e.StartTime < y.e.EndTime && y.e.StartTime < x.e.EndTime)
                    warnings.Add(new("overlap", $"Entries {x.i + 1} and {y.i + 1} overlap in time ({x.e.Description} / {y.e.Description}).", y.i + 1));
            }
        }
        if (entered is int u && u != estimated)
            warnings.Add(new("units_mismatch",
                $"{u} timed units entered, but {timedMinutes} timed minutes give {estimated} under the {variant} rule."));

        return new FlowsheetSummaryDto(timedMinutes, performed.Count(x => !x.e.IsTimed), estimated, entered, variant.ToString(), warnings);
    }
}
