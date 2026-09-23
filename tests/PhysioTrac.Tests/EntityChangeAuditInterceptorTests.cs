using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Auditing;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Tests;

/// <summary>Proves entity changes are audited automatically -- with no
/// per-service opt-in call -- for any entity carrying its own
/// OrganizationId, and that the audit metadata never carries field values
/// (only which properties changed).</summary>
public class EntityChangeAuditInterceptorTests
{
    private static PhysioTracDbContext NewDbWithInterceptor(TestCurrentUser actor)
    {
        var options = new DbContextOptionsBuilder<PhysioTracDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new EntityChangeAuditInterceptor(actor))
            .Options;
        return new PhysioTracDbContext(options);
    }

    [Fact]
    public async Task CreatingAPatient_WritesAnEntityCreatedAuditEvent_WithNoOptInCall()
    {
        var org = new Organization { Name = "Client A", Slug = "client-a" };
        var actor = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        var db = NewDbWithInterceptor(actor);
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var patient = new Patient { OrganizationId = org.Id, FirstName = "Taylor", LastName = "Brooks", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var events = await db.AuditEvents.Where(e => e.ObjectId == patient.Id).ToListAsync();
        var created = Assert.Single(events);
        Assert.Equal("entity.created", created.Action);
        Assert.Equal(nameof(Patient), created.ObjectType);
        Assert.Equal(org.Id, created.OrganizationId);
        Assert.Equal(actor.UserId, created.ActorId);
    }

    [Fact]
    public async Task UpdatingAPatient_WritesAnEntityUpdatedAuditEvent_NamingChangedFields_NotValues()
    {
        var org = new Organization { Name = "Client A", Slug = "client-a" };
        var patient = new Patient { OrganizationId = org.Id, FirstName = "Taylor", LastName = "Brooks", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "555-0100" };
        var actor = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        var db = NewDbWithInterceptor(actor);
        db.Organizations.Add(org);
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        patient.Phone = "555-0199";
        await db.SaveChangesAsync();

        var updated = await db.AuditEvents.Where(e => e.ObjectId == patient.Id && e.Action == "entity.updated").ToListAsync();
        var evt = Assert.Single(updated);
        Assert.Contains("Phone", evt.MetadataJson);
        // The metadata is field NAMES only -- the actual new phone number
        // value must never appear in the audit row.
        Assert.DoesNotContain("555-0199", evt.MetadataJson);

        using var doc = JsonDocument.Parse(evt.MetadataJson);
        var changedProperties = doc.RootElement.GetProperty("changedProperties").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("Phone", changedProperties);
        Assert.DoesNotContain("RowVersion", changedProperties);
        Assert.DoesNotContain("UpdatedAt", changedProperties);
    }

    [Fact]
    public async Task DeletingAPatient_WritesAnEntityDeletedAuditEvent()
    {
        var org = new Organization { Name = "Client A", Slug = "client-a" };
        var patient = new Patient { OrganizationId = org.Id, FirstName = "Taylor", LastName = "Brooks", DateOfBirth = new DateOnly(1990, 1, 1) };
        var actor = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        var db = NewDbWithInterceptor(actor);
        db.Organizations.Add(org);
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        db.Patients.Remove(patient);
        await db.SaveChangesAsync();

        var events = await db.AuditEvents.Where(e => e.ObjectId == patient.Id && e.Action == "entity.deleted").ToListAsync();
        Assert.Single(events);
    }

    [Fact]
    public async Task AuditEventItself_IsNeverAudited_NoInfiniteRecursion()
    {
        var org = new Organization { Name = "Client A", Slug = "client-a" };
        var actor = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        var db = NewDbWithInterceptor(actor);
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        db.AuditEvents.Add(new AuditEvent { OrganizationId = org.Id, Action = "access.denied", ObjectType = "Patient" });
        await db.SaveChangesAsync();

        var events = await db.AuditEvents.ToListAsync();
        // Exactly the one manually-written row -- not a second row auditing
        // the creation of the audit row itself.
        Assert.Single(events);
    }
}
