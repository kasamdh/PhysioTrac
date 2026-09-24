using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

public record ClinicalNoteDto(
    Guid Id, Guid PatientId, Guid TherapistId, Guid? AppointmentId,
    NoteType NoteType, NoteStatus Status, DateOnly ServiceDate,
    string? Subjective, string? Objective, string? Interventions, string? Assessment, string? Plan,
    DateOnly? PlanOfCareStart, DateOnly? PlanOfCareEnd, int? FrequencyPerWeek, int? DurationWeeks, DateOnly? ReassessmentDue,
    string? SignatureName, string? SignatureCredentials, DateTimeOffset? SignedAt, string? SignatureIpAddress, string? SignatureHash,
    bool CosignRequired, Guid? CosignedById, DateTimeOffset? CosignedAt);

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
