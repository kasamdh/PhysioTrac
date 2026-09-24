using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

/// <summary>The configurable clinical-note template engine: definitions
/// (JSON schema of sections/fields) with versioning, and most-specific-wins
/// resolution (Location -> State -> Organization -> Platform default).</summary>
public interface IClinicalTemplateService
{
    /// <summary>Creates version 1 for a scope this org/state/location/note-
    /// type combination has never had a template for, or version N+1 if one
    /// already exists -- the previous version is deactivated (IsActive =
    /// false), never overwritten or deleted, preserving it in history.
    /// Scope == Platform requires RequirePlatformSuperAdmin; every other
    /// scope is OrganizationAdministration-gated and always creates within
    /// the actor's own organization, never a client-supplied one.</summary>
    Task<ClinicalNoteTemplate> CreateAsync(CreateClinicalNoteTemplateRequest request, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>All versions (active and superseded) for the caller's own
    /// organization, plus the org-independent Platform defaults -- the
    /// full history "with versioning" asks for.</summary>
    Task<IReadOnlyList<ClinicalNoteTemplate>> ListAsync(ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Most-specific-wins resolution among ACTIVE templates only:
    /// a Location match (if locationId given) beats a State match (if state
    /// given) beats the org's own Organization-scope default beats the
    /// global Platform default. Returns null if nothing at any level is
    /// configured -- callers fall back to a plain, template-less note.</summary>
    Task<ClinicalNoteTemplate?> ResolveAsync(ICurrentUser actor, NoteType noteType, Guid? locationId, string? state, CancellationToken ct = default);
}
