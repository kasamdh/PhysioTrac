using System.Text.Json;
using PhysioTrac.Application.Audit;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly PhysioTracDbContext _db;

    public AuditService(PhysioTracDbContext db)
    {
        _db = db;
    }

    public async Task RecordAuditEventAsync(
        Guid? actorId,
        string action,
        string objectType,
        Guid? objectId,
        Guid organizationId,
        Guid? patientId = null,
        string? ipAddress = null,
        object? metadata = null,
        CancellationToken ct = default)
    {
        if (organizationId == Guid.Empty)
        {
            throw new InvalidOperationException("record_audit_event requires a resolvable organization.");
        }

        await WriteAsync(actorId, action, objectType, objectId, organizationId, patientId, ipAddress, metadata, ct);
    }

    public async Task RecordPlatformAuditEventAsync(
        Guid? actorId,
        string action,
        string objectType,
        Guid? objectId,
        string? ipAddress = null,
        object? metadata = null,
        CancellationToken ct = default)
    {
        await WriteAsync(actorId, action, objectType, objectId, null, null, ipAddress, metadata, ct);
    }

    private async Task WriteAsync(
        Guid? actorId, string action, string objectType, Guid? objectId, Guid? organizationId,
        Guid? patientId, string? ipAddress, object? metadata, CancellationToken ct)
    {
        _db.AuditEvents.Add(new AuditEvent
        {
            ActorId = actorId,
            Action = action,
            ObjectType = objectType,
            ObjectId = objectId,
            OrganizationId = organizationId,
            PatientId = patientId,
            IpAddress = ipAddress,
            MetadataJson = JsonSerializer.Serialize(metadata ?? new object()),
        });
        await _db.SaveChangesAsync(ct);
    }
}
