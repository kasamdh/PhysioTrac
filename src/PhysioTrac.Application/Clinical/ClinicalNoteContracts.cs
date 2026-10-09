using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

public record ClinicalNoteDto(
    Guid Id, Guid PatientId, Guid TherapistId, Guid? AppointmentId,
    NoteType NoteType, NoteStatus Status, DateOnly ServiceDate,
    string? Subjective, string? Objective, string? Interventions, string? Assessment, string? Plan,
    DateOnly? PlanOfCareStart, DateOnly? PlanOfCareEnd, int? FrequencyPerWeek, int? DurationWeeks, DateOnly? ReassessmentDue,
    DateOnly? PlanOfCareCertifiedDate, Guid? PlanOfCareCertifyingProviderId,
    string? SignatureName, string? SignatureCredentials, DateTimeOffset? SignedAt, string? SignatureIpAddress, string? SignatureHash,
    bool CosignRequired, Guid? CosignedById, DateTimeOffset? CosignedAt,
    string SubjectiveDetailsJson = "{}", string ObjectiveMeasurementsJson = "{}",
    Guid? AmendsNoteId = null, string? AmendmentReason = null,
    Guid? TreatingProviderId = null, Guid? SupervisingProviderId = null, Guid? TemplateVersionId = null, Guid? PlanOfCareId = null,
    DateOnly? PeriodStart = null, DateOnly? PeriodEnd = null, string? ReturnReason = null, string? VoidReason = null,
    DateTimeOffset? PrefilledAt = null, DateTimeOffset? PrefillReviewedAt = null,
    DocumentationStatus DocumentationStatus = DocumentationStatus.Draft, DateTimeOffset? VoidedAt = null);

/// <summary>An electronic signature on a note, as recorded at signing.</summary>
public record ElectronicSignatureDto(
    Guid Id, Guid SignerUserId, string SignerName, string? Credentials, string Role, SignatureMeaning Meaning,
    DateTimeOffset SignedAt, string DisplayTimeZone, int NoteVersionNumber, string? LocalSignedAt = null);

public record NoteStatusChangeDto(Guid Id, NoteStatus? FromStatus, NoteStatus ToStatus, Guid ChangedById, string? ChangedByName, string? Reason, DateTimeOffset ChangedAt);

/// <summary>A note's signature and status history, oldest first.</summary>
public record NoteHistoryDto(IReadOnlyList<ElectronicSignatureDto> Signatures, IReadOnlyList<NoteStatusChangeDto> StatusChanges);

public record PlanOfCareDto(
    Guid Id, Guid PatientId, Guid SourceNoteId, Guid? PreviousPlanOfCareId, PlanOfCareStatus Status,
    DateOnly StartDate, DateOnly EndDate, int? FrequencyPerWeek, int? DurationWeeks,
    string? TreatmentDiagnosis, string? Prognosis, string? RehabPotential, string? PlannedInterventions,
    string? HomeProgram, string? PatientEducation, string? Referrals,
    DateOnly? CertifiedDate, Guid? CertifyingProviderId, DischargeReason? DischargeReason, Guid? DischargeNoteId);

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

public record ClinicalNoteVersionDto(Guid Id, Guid NoteId, int VersionNumber, string ContentJson, Guid SavedById, bool IsSignedVersion, DateTimeOffset CreatedAt, string? SavedByName = null);

public record CreateNoteRequest(
    Guid PatientId, NoteType NoteType, DateOnly ServiceDate, Guid? AppointmentId,
    string? Subjective, string? Objective, string? Interventions, string? Assessment, string? Plan,
    DateOnly? PlanOfCareStart, DateOnly? PlanOfCareEnd, int? FrequencyPerWeek, int? DurationWeeks, DateOnly? ReassessmentDue,
    // Clinical Charting's structured findings (JSON objects); null = none / unchanged.
    string? SubjectiveDetailsJson = null, string? ObjectiveMeasurementsJson = null,
    // The documentation template to write it with; null = the suggested one.
    Guid? TemplateId = null);

public record UpdateNoteRequest(
    string? Subjective, string? Objective, string? Interventions, string? Assessment, string? Plan,
    DateOnly? PlanOfCareStart, DateOnly? PlanOfCareEnd, int? FrequencyPerWeek, int? DurationWeeks, DateOnly? ReassessmentDue,
    string? SubjectiveDetailsJson = null, string? ObjectiveMeasurementsJson = null);

public record NoteAddendumDto(Guid Id, Guid NoteId, Guid AuthorId, string Reason, string Body, DateTimeOffset CreatedAt, string? AuthorName = null);

public record CreateAmendmentRequest(string Reason);

/// <summary>What the current user may do with this note right now -- the
/// service's own rules, so the UI never re-derives them.</summary>
/// <summary>What the caller may do with a note now. SignSubmitsForCosign:
/// the caller's signature sends it to a PT for cosignature. VoidNeedsPassword:
/// voiding a signed note re-confirms the voider's identity.</summary>
public record NoteActionsDto(bool CanEdit, bool CanSign, bool CanCosign, bool CanAddAddendum, bool CanAmend, bool CanLock,
    bool CanStartReview = false, bool CanReturn = false, bool CanVoid = false, bool SignSubmitsForCosign = false,
    bool VoidNeedsPassword = false);

/// <summary>The note's legal record around its content: who wrote and
/// cosigned it, its addenda, and its amendment links.</summary>
public record NoteRecordDto(
    NoteActionsDto Actions,
    string AuthorName,
    string? CosignedByName,
    IReadOnlyList<NoteAddendumDto> Addenda,
    Guid? AmendmentNoteId,
    NoteStatus? AmendmentStatus);

/// <summary>One row in a documentation work queue.</summary>
public record NoteQueueItemDto(
    Guid NoteId, Guid PatientId, string PatientName, string MedicalRecordNumber,
    NoteType NoteType, NoteStatus Status, DateOnly ServiceDate, string AuthorName, bool IsAmendment);

/// <summary>Notes needing the current user's attention: their own unsigned
/// notes, and other clinicians' notes awaiting their cosignature.</summary>
public record NoteQueuesDto(IReadOnlyList<NoteQueueItemDto> MyUnsignedNotes, IReadOnlyList<NoteQueueItemDto> AwaitingMyCosign);

public record CreateAddendumRequest(string Reason, string Body);

public record InterventionDto(Guid Id, Guid NoteId, string Description, string? BodyRegion, InterventionCategory? Category, int Minutes, int? Units, bool IsTimed, int Order, string? PatientResponse = null);

public record CreateInterventionRequest(string Description, string? BodyRegion, InterventionCategory? Category, int Minutes, int? Units, bool IsTimed, int Order, string? PatientResponse = null);

/// <summary>Billing view of a note's interventions: total timed minutes and
/// the units the organization's 8-minute-rule variant gives for them.</summary>
public record InterventionSummaryDto(int TimedMinutes, int UntimedCount, int EstimatedTimedUnits, string RuleVariant);

public record ReturnNoteRequest(string Reason);

/// <param name="Password">Required to void a signed note (step-up).</param>
public record VoidNoteRequest(string Reason, string? Password = null);
