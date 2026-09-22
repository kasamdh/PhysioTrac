using Microsoft.EntityFrameworkCore;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;
using Xunit;

namespace PhysioTrac.Tests;

/// <summary>Mirrors the original `AuditEvent.save()`/`delete()` overrides,
/// which raise on any attempted mutation of an existing row.</summary>
public class AuditEventAppendOnlyTests
{
    private static PhysioTracDbContext NewDb() => new(
        new DbContextOptionsBuilder<PhysioTracDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task UpdatingAnExistingAuditEvent_Throws()
    {
        using var db = NewDb();
        var evt = new AuditEvent { Action = "access.denied", ObjectType = "Patient", OrganizationId = Guid.NewGuid() };
        db.AuditEvents.Add(evt);
        await db.SaveChangesAsync();

        evt.Action = "tampered";

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DeletingAnExistingAuditEvent_Throws()
    {
        using var db = NewDb();
        var evt = new AuditEvent { Action = "access.denied", ObjectType = "Patient", OrganizationId = Guid.NewGuid() };
        db.AuditEvents.Add(evt);
        await db.SaveChangesAsync();

        db.AuditEvents.Remove(evt);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}
