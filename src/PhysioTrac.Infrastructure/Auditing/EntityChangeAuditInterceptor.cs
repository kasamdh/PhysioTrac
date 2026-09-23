using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Infrastructure.Auditing;

/// <summary>Automatically writes an AuditEvent for every create/update/delete
/// of a tenant-scoped entity, without any per-service opt-in call. Scoped
/// deliberately to entities that carry their own `OrganizationId` property
/// (Patient, Provider, ReferringProvider, etc.) -- entities that are only
/// scoped indirectly through a relation (Appointment/ClinicalNote/Charge via
/// PatientId) are NOT covered here, since resolving that would mean extra
/// queries inside the SaveChanges hot path; those already get audited at
/// the point of use where relevant (e.g. TenantAccessService's
/// "access.denied" events).
///
/// Metadata never includes field VALUES, only the names of changed
/// properties (RowVersion/CreatedAt/UpdatedAt excluded as pure noise) --
/// matching the same PHI-safety stance as PhiRedactingDestructuringPolicy
/// and IAuditService's own doc comment ("metadata must never contain PHI").
///
/// This is registered via AddInterceptors (see DependencyInjection), not by
/// adding a constructor parameter to PhysioTracDbContext -- every existing
/// test builds that DbContext directly with just DbContextOptions, and
/// adding a required ICurrentUser dependency there would break all of them.
/// An interceptor resolved from DI keeps the DbContext itself actor-agnostic.</summary>
public class EntityChangeAuditInterceptor : SaveChangesInterceptor
{
    private static readonly string[] IgnoredPropertyNames = { "RowVersion", nameof(BaseEntity.CreatedAt), nameof(BaseEntity.UpdatedAt) };

    private readonly ICurrentUser _currentUser;

    public EntityChangeAuditInterceptor(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        CollectAuditEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        CollectAuditEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void CollectAuditEvents(DbContext? context)
    {
        if (context is null) return;

        var actorId = _currentUser.IsAuthenticated && _currentUser.UserId != Guid.Empty ? _currentUser.UserId : (Guid?)null;
        var newEvents = new List<AuditEvent>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditEvent) continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            if (entry.Entity is not BaseEntity baseEntity) continue;

            var organizationId = GetOrganizationId(entry);
            if (organizationId is null) continue;

            var action = entry.State switch
            {
                EntityState.Added => "entity.created",
                EntityState.Modified => "entity.updated",
                _ => "entity.deleted",
            };

            object metadata = entry.State == EntityState.Modified
                ? new
                {
                    changedProperties = entry.Properties
                        .Where(p => p.IsModified && !IgnoredPropertyNames.Contains(p.Metadata.Name))
                        .Select(p => p.Metadata.Name)
                        .ToArray(),
                }
                : new { };

            newEvents.Add(new AuditEvent
            {
                ActorId = actorId,
                Action = action,
                ObjectType = entry.Entity.GetType().Name,
                ObjectId = baseEntity.Id,
                OrganizationId = organizationId,
                MetadataJson = JsonSerializer.Serialize(metadata),
            });
        }

        foreach (var auditEvent in newEvents)
        {
            context.Add(auditEvent);
        }
    }

    private static Guid? GetOrganizationId(EntityEntry entry)
    {
        var property = entry.Metadata.FindProperty("OrganizationId");
        if (property is null) return null;

        var value = entry.State == EntityState.Deleted
            ? entry.OriginalValues[property]
            : entry.CurrentValues[property];

        return value as Guid?;
    }
}
