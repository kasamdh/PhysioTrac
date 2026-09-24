using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Consents;

public record ConsentTemplateDto(
    Guid Id, ConsentType ConsentType, TemplateScope Scope, string? State, Guid? LocationId,
    string BodyText, int Version, bool IsActive);

public record CreateConsentTemplateRequest(
    ConsentType ConsentType, TemplateScope Scope, string? State, Guid? LocationId, string BodyText);
