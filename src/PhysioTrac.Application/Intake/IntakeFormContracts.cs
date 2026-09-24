using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Intake;

public record IntakeFormTemplateDto(
    Guid Id, string Key, TemplateScope Scope, string? State, Guid? LocationId,
    string Name, string SchemaJson, int Version, bool IsActive);

public record CreateIntakeFormTemplateRequest(
    string Key, TemplateScope Scope, string? State, Guid? LocationId, string Name, string SchemaJson);

public record IntakeFormSubmissionDto(
    Guid Id, Guid PatientId, Guid IntakeFormTemplateId, int TemplateVersion, string ResponseJson,
    IntakeFormSubmissionStatus Status, DateTimeOffset SubmittedAt, Guid? ReviewedById, DateTimeOffset? ReviewedAt);

public record SubmitIntakeFormRequest(Guid IntakeFormTemplateId, string ResponseJson);
