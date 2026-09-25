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

/// <summary>CurrentPassword is the reauthentication step: even an already-
/// logged-in super admin must re-prove who they are immediately before
/// gaining break-glass clinical access, the same "step-up auth" pattern a
/// bank might require before a high-risk action.</summary>
public record RequestPrivilegedAccessRequest(string Reason, int DurationHours, string CurrentPassword);
