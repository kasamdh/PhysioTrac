using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

public record ClinicalNoteTemplateDto(
    Guid Id, NoteType NoteType, TemplateScope Scope, string? State, Guid? LocationId,
    string Name, string SchemaJson, int Version, bool IsActive);

public record CreateClinicalNoteTemplateRequest(
    NoteType NoteType, TemplateScope Scope, string? State, Guid? LocationId, string Name, string SchemaJson);
