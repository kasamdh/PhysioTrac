namespace PhysioTrac.Application.Audit;

public interface IAuditService
{
    /// <summary>Writes one append-only audit row. Requires a resolvable
    /// organization for any tenant-scoped action — throws if one can't be
    /// determined. <paramref name="metadata"/> must never contain PHI or
    /// clinical narrative text, only ids/actions/routes.</summary>
    Task RecordAuditEventAsync(
        Guid? actorId,
        string action,
        string objectType,
        Guid? objectId,
        Guid organizationId,
        Guid? patientId = null,
        string? ipAddress = null,
        object? metadata = null,
        CancellationToken ct = default);

    /// <summary>For genuinely platform-wide events with no single owning
    /// tenant (e.g. a super admin changing platform-level defaults).</summary>
    Task RecordPlatformAuditEventAsync(
        Guid? actorId,
        string action,
        string objectType,
        Guid? objectId,
        string? ipAddress = null,
        object? metadata = null,
        CancellationToken ct = default);
}
