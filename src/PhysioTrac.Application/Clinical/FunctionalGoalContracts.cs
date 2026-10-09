using System.Text.RegularExpressions;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

public record FunctionalGoalDto(
    Guid Id, Guid PatientId, Guid AuthorId, string FunctionalLimitation, string FunctionalTask, GoalTerm Term,
    decimal BaselineValue, decimal TargetValue, decimal? CurrentValue, string Unit, string MeasurementMethod,
    DateOnly TargetDate, GoalStatus Status, int? ProgressPercent, Guid? ApprovedById, DateTimeOffset? ApprovedAt,
    Guid? PlanOfCareId = null, string? Comments = null, int Version = 1);

public record CreateGoalRequest(
    Guid PatientId, string FunctionalLimitation, string FunctionalTask, GoalTerm Term,
    decimal BaselineValue, decimal TargetValue, string Unit, string MeasurementMethod,
    DateOnly TargetDate, string? SuggestedWording, string? Comments = null);

/// <summary>Edits a goal's definition. The previous version stays in the
/// goal's history, and notes that documented it keep their own copy.</summary>
public record UpdateGoalRequest(
    string FunctionalLimitation, string FunctionalTask, GoalTerm Term,
    decimal BaselineValue, decimal TargetValue, string Unit, string MeasurementMethod,
    DateOnly TargetDate, string? Comments);

/// <summary>Progress recorded outside a note. A null value or status
/// leaves it unchanged.</summary>
public record UpdateGoalProgressRequest(decimal? CurrentValue, GoalStatus? Status = null, string? Comment = null);

/// <summary>A goal's definition at one point in its history.</summary>
public record GoalSnapshotDto(
    GoalTerm Term, string FunctionalTask, string FunctionalLimitation, decimal BaselineValue, decimal TargetValue,
    string Unit, string MeasurementMethod, DateOnly TargetDate, string? Comments);

public record GoalHistoryDto(
    Guid Id, GoalHistoryKind Kind, int GoalVersion, Guid? NoteId, DateOnly? NoteServiceDate, Guid RecordedById,
    string? RecordedByName, DateTimeOffset RecordedAt, GoalStatus Status, decimal? CurrentValue, int? ProgressPercent,
    string? Comment, GoalSnapshotDto Snapshot);

/// <summary>A goal's progress on one note. On save the client sends
/// GoalId, CurrentValue, Status, Comment and IncludeInNarrative; the goal
/// as it reads now (and its previous value) is copied in by the server, and
/// kept as written once the note is signed.</summary>
public record NoteGoalProgressDto(
    Guid GoalId, decimal? CurrentValue, GoalStatus Status, string? Comment, bool IncludeInNarrative = true,
    int GoalVersion = 0, GoalTerm Term = GoalTerm.ShortTerm, string? FunctionalTask = null, string? FunctionalLimitation = null,
    decimal BaselineValue = 0, decimal TargetValue = 0, string? Unit = null, string? MeasurementMethod = null,
    DateOnly? TargetDate = null, decimal? PreviousValue = null, int? ProgressPercent = null, Guid? Id = null);

public static class GoalRules
{
    public const int MaxText = 500;

    /// <summary>Statuses a clinician can record (Draft is only the state
    /// before approval).</summary>
    public static bool IsRecordable(GoalStatus status) =>
        status is GoalStatus.NotStarted or GoalStatus.Active or GoalStatus.Met or GoalStatus.PartiallyMet or GoalStatus.Discontinued;

    public static bool IsOpen(GoalStatus status) =>
        status is GoalStatus.Draft or GoalStatus.NotStarted or GoalStatus.Active;

    /// <summary>Hard rules for a goal's definition.</summary>
    public static IReadOnlyList<string> Validate(string functionalTask, string unit, decimal baseline, decimal target)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(functionalTask)) errors.Add("Describe the functional goal.");
        else if (functionalTask.Length > MaxText) errors.Add($"The goal is longer than {MaxText} characters.");
        if (string.IsNullOrWhiteSpace(unit)) errors.Add("Give the unit the goal is measured in.");
        if (baseline == target) errors.Add("The target must differ from the baseline.");
        return errors;
    }

    private static readonly Regex Vague = new(@"\b(improve[sd]?|increase[sd]?|decrease[sd]?|reduce[sd]?|better|enhance[sd]?|maximi[sz]e)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Advice (never blocking) toward functional, measurable,
    /// time-bound wording.</summary>
    public static IReadOnlyList<string> WordingAdvice(string functionalTask, string? measurementMethod, DateOnly targetDate, DateOnly today)
    {
        var advice = new List<string>();
        var task = functionalTask?.Trim() ?? string.Empty;
        if (task.Length == 0) return advice;
        if (Vague.IsMatch(task) && !Regex.IsMatch(task, @"\d"))
            advice.Add("Say what the patient will be able to do and how much (e.g. \"climb 12 stairs with one rail\"), not only what will improve.");
        if (!Regex.IsMatch(task, @"\d"))
            advice.Add("Include a measurable amount in the goal (distance, time, repetitions, score, assistance level).");
        if (string.IsNullOrWhiteSpace(measurementMethod))
            advice.Add("Say how the goal will be measured.");
        if (targetDate < today)
            advice.Add("The target date has passed — update it or record the goal's status.");
        return advice;
    }
}
