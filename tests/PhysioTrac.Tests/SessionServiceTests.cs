using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhysioTrac.Application.Configuration;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;
using Xunit;

namespace PhysioTrac.Tests;

/// <summary>Mirrors `SessionManagementTests` from the original suite: per-
/// role concurrency limits (oldest session revoked first) and idle timeout.</summary>
public class SessionServiceTests
{
    private static (PhysioTracDbContext Db, SessionService Service) NewService(SecurityOptions? options = null)
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var service = new SessionService(db, Microsoft.Extensions.Options.Options.Create(options ?? new SecurityOptions()));
        return (db, service);
    }

    [Fact]
    public async Task CreateSession_ExceedingRoleLimit_RevokesOldestFirst()
    {
        var (db, service) = NewService(new SecurityOptions
        {
            SessionLimitsByRole = new Dictionary<string, int> { ["Admin"] = 1 },
        });
        var userId = Guid.NewGuid();

        var first = await service.CreateSessionAsync(userId, Guid.NewGuid(), UserRole.Admin, "1.1.1.1", "ua-1");
        var second = await service.CreateSessionAsync(userId, Guid.NewGuid(), UserRole.Admin, "2.2.2.2", "ua-2");

        var active = await service.ListActiveForUserAsync(userId);

        Assert.Single(active);
        Assert.Equal(second.Id, active[0].Id);
    }

    [Fact]
    public async Task ValidateAndTouch_PastIdleTimeout_RevokesAndReturnsNull()
    {
        var (db, service) = NewService(new SecurityOptions { IdleTimeoutMinutes = 15 });
        var userId = Guid.NewGuid();
        var session = await service.CreateSessionAsync(userId, Guid.NewGuid(), UserRole.Therapist, null, null);

        // Simulate a session that has been idle for longer than the limit.
        var tracked = db.UserSessions.First(s => s.Id == session.Id);
        tracked.LastActivityAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        await db.SaveChangesAsync();

        var result = await service.ValidateAndTouchAsync(session.SessionKey);

        Assert.Null(result);
        var revoked = await db.UserSessions.FirstAsync(s => s.Id == session.Id);
        Assert.Equal(SessionRevokedReason.IdleTimeout, revoked.RevokedReason);
    }

    [Fact]
    public async Task ValidateAndTouch_WithinLimits_TouchesLastActivity()
    {
        var (db, service) = NewService();
        var session = await service.CreateSessionAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.Therapist, null, null);
        var originalActivity = session.LastActivityAt;

        await Task.Delay(10);
        var result = await service.ValidateAndTouchAsync(session.SessionKey);

        Assert.NotNull(result);
        Assert.True(result!.LastActivityAt > originalActivity);
    }
}
