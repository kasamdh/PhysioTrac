using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>A configurable, versioned intake-form definition -- the form-
/// builder counterpart of <see cref="ClinicalNoteTemplate"/>, reusing the
/// exact same scoping (Platform/Organization/State/Location, most-specific-
/// wins resolution) and versioning rule (a new version for the same scope
/// tuple deactivates the previous one rather than overwriting it).
///
/// Keyed by <see cref="Key"/> (a short slug like "new-patient-intake")
/// rather than a fixed enum like ClinicalNoteTemplate's NoteType, since an
/// organization can define any number of differently-purposed intake forms
/// -- there's no fixed list of "kinds" the way there is for clinical notes.
///
/// SchemaJson's shape mirrors ClinicalNoteTemplate's own documented (not
/// enforced field-by-field) convention: a JSON object describing sections/
/// fields for a frontend form renderer to interpret -- the backend only
/// validates it's well-formed JSON and owns resolution/versioning, exactly
/// like the clinical template engine.</summary>
public class IntakeFormTemplate : BaseEntity
{
    /// <summary>Null only for Scope == Platform.</summary>
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public string Key { get; set; } = string.Empty;
    public TemplateScope Scope { get; set; }

    /// <summary>Two-letter USPS state code -- set only for Scope == State.</summary>
    public string? State { get; set; }

    /// <summary>Set only for Scope == Location.</summary>
    public Guid? LocationId { get; set; }
    public Location? LocationDetail { get; set; }

    public string Name { get; set; } = string.Empty;
    public string SchemaJson { get; set; } = "{}";

    /// <summary>Starts at 1; a new version for the same (Scope,
    /// OrganizationId, State, LocationId, Key) tuple increments this and
    /// deactivates the previous row rather than overwriting it.</summary>
    public int Version { get; set; } = 1;

    /// <summary>False once superseded by a newer version.</summary>
    public bool IsActive { get; set; } = true;

    public Guid CreatedById { get; set; }
}
