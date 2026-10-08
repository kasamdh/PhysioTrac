using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

/// <summary>Appointments in the period: attended (checked in, in progress
/// or completed), cancelled and no-shows.</summary>
public record AttendanceDto(int Attended, int Cancelled, int NoShows, string Text);

/// <summary>A summary of the patient's episode built only from signed
/// ("approved") charting -- the source for pre-filling a progress note,
/// re-evaluation, recertification or discharge summary.</summary>
public record EpisodeSummaryDto(
    NoteType NoteType,
    DateOnly EpisodeStart, DateOnly PeriodStart, DateOnly PeriodEnd, string PeriodBasis,
    Guid? PlanOfCareId, DateOnly? PlanStart, DateOnly? PlanEnd, int? FrequencyPerWeek, int? DurationWeeks,
    int VisitsInPeriod, int VisitsInEpisode, AttendanceDto Attendance,
    string? PainSummary,
    IReadOnlyList<string> MeasurementChanges,
    IReadOnlyList<string> OutcomeChanges,
    IReadOnlyList<string> GoalLines,
    Guid? EvaluationNoteId, DateOnly? EvaluationDate, IReadOnlyList<string> SinceEvaluation,
    Guid? PreviousProgressNoteId, DateOnly? PreviousProgressDate, IReadOnlyList<string> SinceProgress,
    string? HomeProgram,
    int SignedNotesUsed);

/// <summary>Pre-fill request: the editor's save version (like an encounter
/// save, it is refused if someone saved in between).</summary>
public record PrefillNoteRequest(int BaseSaveVersion);

public record PrefillResultDto(int SaveVersion, IReadOnlyList<string> FilledFields, int GoalsAdded, EpisodeSummaryDto Summary);

public static class EpisodeRules
{
    /// <summary>Note types that are written from the episode's charting.</summary>
    public static bool IsSummaryNote(NoteType type) =>
        type is NoteType.Progress or NoteType.ReEvaluation or NoteType.Recertification or NoteType.Discharge;

    /// <summary>Visit notes count toward visits completed (not calls,
    /// missed-visit notes, addenda or consultations).</summary>
    public static bool IsVisit(NoteType type) =>
        type is not (NoteType.Communication or NoteType.MissedVisit or NoteType.Addendum or NoteType.Consultation);

    /// <summary>Maps a discharge summary's "Reason for discharge" choice.</summary>
    public static DischargeReason? DischargeReasonFor(string? text) => text?.Trim() switch
    {
        "Goals met" => DischargeReason.GoalsMet,
        "Maximum benefit achieved" => DischargeReason.MaximumBenefitAchieved,
        "Patient request" => DischargeReason.PatientRequest,
        "Nonattendance" => DischargeReason.Nonattendance,
        "Medical change" => DischargeReason.MedicalChange,
        "Referred elsewhere" => DischargeReason.ReferredElsewhere,
        "Authorization limitation" => DischargeReason.AuthorizationLimitation,
        "Other documented reason" => DischargeReason.Other,
        _ => null,
    };
}
