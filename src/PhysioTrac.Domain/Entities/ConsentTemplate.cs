using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>A configurable, versioned body of consent language for a given
/// <see cref="ConsentType"/> -- the consent-side counterpart of
/// <see cref="ClinicalNoteTemplate"/>, same scoping (Platform/Organization/
/// State/Location, most-specific-wins resolution) and same versioning rule
/// (a new version for the same scope tuple deactivates the previous one
/// rather than overwriting it, so exactly what a patient agreed to on a
/// given date stays reconstructable). See ClinicalNoteTemplate's own doc
/// comment for the full scoping-rule writeup, which applies here unchanged.
///
/// <see cref="ConsentTypeText"/>'s static strings remain the ultimate
/// fallback when no template has been configured for an organization (e.g.
/// a brand-new tenant, or any org before this template engine existed) --
/// ConsentService.RecordAsync resolves a template first and only falls back
/// to the static text if resolution comes back empty.</summary>
public class ConsentTemplate : BaseEntity
{
    /// <summary>Null only for Scope == Platform.</summary>
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public ConsentType ConsentType { get; set; }
    public TemplateScope Scope { get; set; }

    /// <summary>Two-letter USPS state code -- set only for Scope == State.</summary>
    public string? State { get; set; }

    /// <summary>Set only for Scope == Location.</summary>
    public Guid? LocationId { get; set; }
    public Location? LocationDetail { get; set; }

    /// <summary>The actual consent language shown to and signed by the
    /// patient -- snapshotted into Consent.ConsentText at signing time, so a
    /// later language change never silently rewrites what someone already
    /// agreed to.</summary>
    public string BodyText { get; set; } = string.Empty;

    /// <summary>Starts at 1; a new version for the same (Scope,
    /// OrganizationId, State, LocationId, ConsentType) tuple increments this
    /// and deactivates the previous row rather than overwriting it.</summary>
    public int Version { get; set; } = 1;

    /// <summary>False once superseded by a newer version. Old versions are
    /// never deleted -- every Consent.TemplateVersion stays resolvable
    /// against the exact language that was in effect when it was signed.</summary>
    public bool IsActive { get; set; } = true;

    public Guid CreatedById { get; set; }
}
