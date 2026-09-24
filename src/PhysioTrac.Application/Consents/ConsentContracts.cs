using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Consents;

public record ConsentDto(
    Guid Id, Guid PatientId, ConsentType ConsentType, string SignedByName,
    Guid RecordedById, DateTimeOffset SignedAt, bool IsActive, DateTimeOffset? RevokedAt, int? TemplateVersion);

public record RecordConsentRequest(Guid PatientId, ConsentType ConsentType, string SignedByName);

/// <summary>Same shape as <see cref="RecordConsentRequest"/> minus PatientId
/// -- the portal resolves the patient from the session, never from client
/// input.</summary>
public record RecordOwnConsentRequest(ConsentType ConsentType, string SignedByName);

public record RevokeConsentRequest(string? Reason);
