using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>Time-boxed, reasoned, fully-audited break-glass access to one
/// client's clinical data for a platform super administrator. Super admins
/// have no standing clinical access; this is the only explicit path in, and
/// every grant and every read taken under it must be audited separately.</summary>
public class PrivilegedAccessGrant : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid ActorId { get; set; }

    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedById { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}
