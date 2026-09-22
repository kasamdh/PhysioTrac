namespace PhysioTrac.Application.SuperAdmin;

public record PrivilegedAccessGrantDto(
    Guid Id,
    string ActorName,
    string Reason,
    DateTimeOffset RequestedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt,
    string? RevokedByName,
    bool IsActive);

public record RequestPrivilegedAccessRequest(string Reason, int DurationHours);
