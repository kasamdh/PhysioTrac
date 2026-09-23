using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Messaging;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

public class MessageService : IMessageService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;

    public MessageService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAuditService audit)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
    }

    public async Task<Message> SendAsync(SendMessageRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.DocumentManagement);
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, request.PatientId, ct: ct);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new InvalidOperationException("A message body is required.");
        }

        var message = new Message
        {
            OrganizationId = organization.Id,
            PatientId = patient.Id,
            SenderId = actor.UserId,
            SenderRole = actor.Role,
            IsFromPatient = false,
            Body = request.Body.Trim(),
        };
        _db.Messages.Add(message);
        await _db.SaveChangesAsync(ct);

        // Never log the message body -- it's free-text and may contain PHI.
        await _audit.RecordAuditEventAsync(actor.UserId, "message.sent", nameof(Message), message.Id,
            organization.Id, patientId: patient.Id, ct: ct);

        return message;
    }

    public async Task<IReadOnlyList<Message>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        return await _db.Messages.Where(m => m.PatientId == patient.Id)
            .OrderBy(m => m.SentAt).ToListAsync(ct);
    }

    public async Task MarkThreadReadAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        var unread = await _db.Messages
            .Where(m => m.PatientId == patient.Id && m.ReadAt == null && m.SenderId != actor.UserId)
            .ToListAsync(ct);

        foreach (var message in unread)
        {
            message.ReadAt = DateTimeOffset.UtcNow;
            message.ReadById = actor.UserId;
            message.UpdatedAt = DateTimeOffset.UtcNow;
        }
        if (unread.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }
    }
}
