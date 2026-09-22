using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.SuperAdmin;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Direct port of `care/privileged_access.py`.</summary>
public class PrivilegedAccessService : IPrivilegedAccessService
{
    private readonly PhysioTracDbContext _db;
    private readonly IAuditService _audit;

    public PrivilegedAccessService(PhysioTracDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<PrivilegedAccessGrant?> ActiveGrantAsync(Guid organizationId, Guid actorId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.PrivilegedAccessGrants
            .Where(g => g.OrganizationId == organizationId && g.ActorId == actorId && g.RevokedAt == null && g.ExpiresAt > now)
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PrivilegedAccessGrant> RequestAsync(Guid organizationId, ICurrentUser actor, string reason, int durationHours, CancellationToken ct = default)
    {
        if (!IPrivilegedAccessService.AllowedDurationsHours.Contains(durationHours))
        {
            throw new InvalidOperationException("Choose a supported access duration.");
        }
        reason = reason.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            throw new InvalidOperationException("A reason is required to request privileged clinical access.");
        }

        var grant = new PrivilegedAccessGrant
        {
            OrganizationId = organizationId,
            ActorId = actor.UserId,
            Reason = reason,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(durationHours),
        };
        _db.PrivilegedAccessGrants.Add(grant);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "privileged_access.requested", nameof(PrivilegedAccessGrant), grant.Id, organizationId,
            metadata: new { reason, durationHours, expiresAt = grant.ExpiresAt }, ct: ct);

        return grant;
    }

    public async Task<PrivilegedAccessGrant> RevokeAsync(Guid grantId, ICurrentUser actor, CancellationToken ct = default)
    {
        var grant = await _db.PrivilegedAccessGrants.FirstOrDefaultAsync(g => g.Id == grantId, ct)
            ?? throw new NotFoundException("Access grant was not found.");
        if (!grant.IsActive)
        {
            throw new InvalidOperationException("This access grant is not active.");
        }

        grant.RevokedAt = DateTimeOffset.UtcNow;
        grant.RevokedById = actor.UserId;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "privileged_access.revoked", nameof(PrivilegedAccessGrant), grant.Id, grant.OrganizationId, ct: ct);
        return grant;
    }

    public async Task<IReadOnlyList<PrivilegedAccessGrant>> ListAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await _db.PrivilegedAccessGrants
            .Where(g => g.OrganizationId == organizationId)
            .OrderByDescending(g => g.CreatedAt)
            .Take(50)
            .ToListAsync(ct);
    }
}
