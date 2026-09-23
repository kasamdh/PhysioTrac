using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Consents;

public record ConsentDto(
    Guid Id, Guid PatientId, ConsentType ConsentType, string SignedByName,
    Guid RecordedById, DateTimeOffset SignedAt, bool IsActive, DateTimeOffset? RevokedAt);

public record RecordConsentRequest(Guid PatientId, ConsentType ConsentType, string SignedByName);

public record RevokeConsentRequest(string? Reason);
