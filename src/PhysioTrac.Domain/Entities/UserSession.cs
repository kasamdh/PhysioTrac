using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>One authenticated browser/device session, tracked independently
/// of the ASP.NET Core auth cookie itself so a login can be revoked, listed,
/// and limited per user regardless of how long the underlying cookie is valid.</summary>
public class UserSession : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    /// <summary>Correlates this row to the auth cookie's session-id claim.</summary>
    public string SessionKey { get; set; } = string.Empty;

    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public SessionRevokedReason RevokedReason { get; set; } = SessionRevokedReason.None;

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceName { get; set; }
    public string? BrowserName { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}
