using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhysioTrac.Application.Configuration;
using PhysioTrac.Application.Sessions;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

public class SessionService : ISessionService
{
    private readonly PhysioTracDbContext _db;
    private readonly SecurityOptions _options;

    public SessionService(PhysioTracDbContext db, IOptions<SecurityOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<UserSession> CreateSessionAsync(
        Guid userId, Guid? organizationId, UserRole role,
        string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var limit = _options.SessionLimitsByRole.TryGetValue(role.ToString(), out var configured) ? configured : 2;

        var active = await _db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

        // Revoke oldest sessions first if adding this one would exceed the limit.
        var toRevoke = active.Count - (limit - 1);
        for (var i = 0; i < toRevoke && i < active.Count; i++)
        {
            active[i].RevokedAt = now;
            active[i].RevokedReason = SessionRevokedReason.NewLogin;
        }

        var session = new UserSession
        {
            UserId = userId,
            OrganizationId = organizationId,
            SessionKey = Guid.NewGuid().ToString("N"),
            LastActivityAt = now,
            ExpiresAt = now.AddHours(_options.AbsoluteSessionHours),
            IpAddress = ipAddress,
            UserAgent = userAgent?.Length > 400 ? userAgent[..400] : userAgent,
        };
        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<UserSession?> ValidateAndTouchAsync(string sessionKey, CancellationToken ct = default)
    {
        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.SessionKey == sessionKey, ct);
        if (session is null || !session.IsActive)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        if (now - session.LastActivityAt > TimeSpan.FromMinutes(_options.IdleTimeoutMinutes))
        {
            session.RevokedAt = now;
            session.RevokedReason = SessionRevokedReason.IdleTimeout;
            await _db.SaveChangesAsync(ct);
            return null;
        }

        if (now - session.CreatedAt > TimeSpan.FromHours(_options.AbsoluteSessionHours))
        {
            session.RevokedAt = now;
            session.RevokedReason = SessionRevokedReason.AbsoluteTimeout;
            await _db.SaveChangesAsync(ct);
            return null;
        }

        session.LastActivityAt = now;
        await _db.SaveChangesAsync(ct);
        return session;
    }

    public async Task RevokeAsync(string sessionKey, SessionRevokedReason reason, CancellationToken ct = default)
    {
        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.SessionKey == sessionKey, ct);
        if (session is null || session.RevokedAt is not null) return;
        session.RevokedAt = DateTimeOffset.UtcNow;
        session.RevokedReason = reason;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UserSession>> ListActiveForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task RevokeAllForUserAsync(Guid userId, SessionRevokedReason reason, string? exceptSessionKey = null, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now
                && (exceptSessionKey == null || s.SessionKey != exceptSessionKey))
            .ToListAsync(ct);
        foreach (var s in sessions)
        {
            s.RevokedAt = now;
            s.RevokedReason = reason;
        }
        await _db.SaveChangesAsync(ct);
    }
}
