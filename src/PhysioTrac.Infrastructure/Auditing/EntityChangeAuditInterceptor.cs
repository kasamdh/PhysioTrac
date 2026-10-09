using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Infrastructure.Auditing;

/// <summary>Automatically writes an AuditEvent for every create/update/delete
/// of a tenant's records, without any per-service opt-in call. Records that
/// carry their own `OrganizationId` (Patient, Provider, Location...) are
/// attributed to it; records scoped through a parent (Appointment,
/// ClinicalNote, ProviderAvailability, NoteIntervention...) are attributed to
/// the signed-in user's organization -- tenant isolation guarantees that is
/// the organization being changed, and it costs no extra query inside the
/// SaveChanges hot path. Changes with no signed-in user and no own
/// OrganizationId (seeding, background jobs) are skipped. The patient, when
/// the record has one, is recorded too, so a patient's history can be
/// reviewed. Sessions and internal history copies are skipped as noise
/// (see IgnoredEntityTypes).
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

    /// <summary>Not logged: sessions are touched on nearly every request
    /// (sign-in / sign-out have their own auth.* events), and version /
    /// status-history rows are internal copies of a change that is already
    /// logged on its parent record.</summary>
    private static readonly HashSet<string> IgnoredEntityTypes = new()
    {
        nameof(UserSession), nameof(ClinicalNoteVersion), nameof(AppointmentStatusHistory),
        // Autosaved note content and the note's own status history: the
        // note itself is logged, and its actions are audited explicitly.
        nameof(ClinicalNoteFieldValue), nameof(ClinicalNoteStatusChange),
    };

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
            if (IgnoredEntityTypes.Contains(entry.Entity.GetType().Name)) continue;

            // Records scoped through a parent (an appointment or note via its
            // patient, provider hours via the provider...) carry no
            // OrganizationId of their own; the signed-in user's organization
            // is the one being changed, since tenant isolation only lets them
            // touch their own organization's records.
            var organizationId = GetOrganizationId(entry)
                ?? (entry.Entity is Organization org ? org.Id : (Guid?)null)
                ?? (actorId is not null ? _currentUser.OrganizationId : null);
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
                PatientId = entry.Entity is Patient ? baseEntity.Id : GetGuid(entry, "PatientId"),
                MetadataJson = JsonSerializer.Serialize(metadata),
            });
        }

        foreach (var auditEvent in newEvents)
        {
            context.Add(auditEvent);
        }
    }

    private static Guid? GetOrganizationId(EntityEntry entry) => GetGuid(entry, "OrganizationId");

    private static Guid? GetGuid(EntityEntry entry, string propertyName)
    {
        var property = entry.Metadata.FindProperty(propertyName);
        if (property is null) return null;

        var value = entry.State == EntityState.Deleted
            ? entry.OriginalValues[property]
            : entry.CurrentValues[property];

        return value as Guid?;
    }
}
