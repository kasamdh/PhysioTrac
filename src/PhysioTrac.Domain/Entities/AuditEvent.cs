namespace PhysioTrac.Domain.Entities;

/// <summary>Append-only audit trail. Metadata must contain no clinical
/// narrative — action/object/ids/IP/route only. Enforced append-only both at
/// the DbContext (SaveChanges override) and the repository/service layer.</summary>
public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Null only for a genuinely platform-wide event with no single
    /// owning tenant. Every tenant-scoped action still requires a real
    /// organization.</summary>
    public Guid? OrganizationId { get; set; }
    public Guid? PatientId { get; set; }
    public Guid? ActorId { get; set; }

    public string Action { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public Guid? ObjectId { get; set; }
    public string? IpAddress { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
