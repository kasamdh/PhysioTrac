using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

public record ClinicalNoteDto(
    Guid Id, Guid PatientId, Guid TherapistId, Guid? AppointmentId,
    NoteType NoteType, NoteStatus Status, DateOnly ServiceDate,
    string? Subjective, string? Objective, string? Interventions, string? Assessment, string? Plan,
    DateOnly? PlanOfCareStart, DateOnly? PlanOfCareEnd, int? FrequencyPerWeek, int? DurationWeeks, DateOnly? ReassessmentDue,
    DateOnly? PlanOfCareCertifiedDate, Guid? PlanOfCareCertifyingProviderId,
    string? SignatureName, string? SignatureCredentials, DateTimeOffset? SignedAt, string? SignatureIpAddress, string? SignatureHash,
    bool CosignRequired, Guid? CosignedById, DateTimeOffset? CosignedAt);

public record CertifyPlanOfCareRequest(DateOnly CertifiedDate, Guid CertifyingProviderId);

/// <summary>What a new note for this patient should pre-populate from the
/// most recent prior documentation -- "pull-forward" of goals,
/// measurements, and diagnoses. A pure read; the caller (the note-creation
/// UI, not built in this pass) decides what to actually copy into the new
/// note's fields.</summary>
public record PullForwardDataDto(
    IReadOnlyList<FunctionalGoalDto> ActiveGoals,
    string? LastObjectiveMeasurementsJson,
    IReadOnlyList<PullForwardDiagnosisDto> ActiveDiagnoses);

public record PullForwardDiagnosisDto(Guid DiagnosisCodeId, string Code, string Description, bool IsPrimary);

/// <summary>Whether a progress note is due for this patient right now, and
/// why -- day-count (the most recent Evaluation/Progress/ReEvaluation
/// note's ReassessmentDue has passed) and/or visit-count (that many signed
/// Daily/Soap/HomeVisit notes have accumulated since then), per
/// Organization.ProgressNoteDueDays/ProgressNoteDueVisitCount. Both reasons
/// can be true at once; neither being configured means this is always false.</summary>
public record ProgressNoteStatusDto(bool IsDue, bool DueByDayCount, bool DueByVisitCount, int VisitsSinceLastProgressNote, DateOnly? ReassessmentDue);

public record ClinicalNoteVersionDto(Guid Id, Guid NoteId, int VersionNumber, string ContentJson, Guid SavedById, bool IsSignedVersion, DateTimeOffset CreatedAt);

public record CreateNoteRequest(
    Guid PatientId, NoteType NoteType, DateOnly ServiceDate, Guid? AppointmentId,
    string? Subjective, string? Objective, string? Interventions, string? Assessment, string? Plan,
    DateOnly? PlanOfCareStart, DateOnly? PlanOfCareEnd, int? FrequencyPerWeek, int? DurationWeeks, DateOnly? ReassessmentDue);

public record UpdateNoteRequest(
    string? Subjective, string? Objective, string? Interventions, string? Assessment, string? Plan,
    DateOnly? PlanOfCareStart, DateOnly? PlanOfCareEnd, int? FrequencyPerWeek, int? DurationWeeks, DateOnly? ReassessmentDue);

public record NoteAddendumDto(Guid Id, Guid NoteId, Guid AuthorId, string Reason, string Body, DateTimeOffset CreatedAt);

public record CreateAddendumRequest(string Reason, string Body);

public record InterventionDto(Guid Id, Guid NoteId, string Description, string? BodyRegion, InterventionCategory? Category, int Minutes, int? Units, bool IsTimed, int Order);

public record CreateInterventionRequest(string Description, string? BodyRegion, InterventionCategory? Category, int Minutes, int? Units, bool IsTimed, int Order);
