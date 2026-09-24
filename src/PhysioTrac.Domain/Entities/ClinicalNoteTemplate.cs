using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>A configurable schema of sections/fields a clinical note of a
/// given NoteType should present -- the backend doesn't interpret
/// SchemaJson's contents beyond validating it's well-formed JSON; rendering
/// it into an actual note-taking form is a frontend concern (deferred, see
/// README). What the backend DOES own: resolution (which template applies
/// to a given organization/location/state) and versioning.
///
/// Scoping rules, enforced in ClinicalTemplateService rather than a DB
/// constraint (the four scopes need different required/forbidden columns):
/// Platform requires OrganizationId/State/LocationId all null (global
/// default, platform-super-admin-only to create); Organization requires
/// OrganizationId set, State/LocationId null; State requires
/// OrganizationId+State set, LocationId null; Location requires
/// OrganizationId+LocationId set, State null.
///
/// SchemaJson's documented shape (informative only, not validated field-by-
/// field): { "sections": [ { "key": "objective", "label": "Objective",
/// "fields": [ { "key": "painScale", "type": "painScale0to10" },
/// { "key": "rom", "type": "romTable" }, { "key": "mmt", "type": "mmtTable" },
/// { "key": "goals", "type": "goalsList" },
/// { "key": "outcomeMeasures", "type": "outcomeMeasureRef" },
/// { "key": "functionalLimitations", "type": "functionalLimitationsList" },
/// { "key": "diagnoses", "type": "icd10Picker" },
/// { "key": "cptCodes", "type": "cptPicker" } ] } ] } -- the field "type"
/// values name the reusable clinical components this phase asks for; each
/// one already has a real, structured backend counterpart (PatientDiagnosis
/// for icd10Picker, NoteIntervention/ServicePrice for cptPicker, OutcomeScore
/// for outcomeMeasureRef, FunctionalGoal for goalsList) rather than the
/// template being the only place that data lives.</summary>
public class ClinicalNoteTemplate : BaseEntity
{
    /// <summary>Null only for Scope == Platform.</summary>
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public NoteType NoteType { get; set; }
    public TemplateScope Scope { get; set; }

    /// <summary>Two-letter USPS state code -- set only for Scope == State.</summary>
    public string? State { get; set; }

    /// <summary>Set only for Scope == Location.</summary>
    public Guid? LocationId { get; set; }
    public Location? LocationDetail { get; set; }

    public string Name { get; set; } = string.Empty;
    public string SchemaJson { get; set; } = "{}";

    /// <summary>Starts at 1; a new version for the same (Scope,
    /// OrganizationId, State, LocationId, NoteType) tuple increments this
    /// and deactivates the previous row rather than overwriting it -- see
    /// ClinicalTemplateService.CreateAsync.</summary>
    public int Version { get; set; } = 1;

    /// <summary>False once superseded by a newer version. Old versions are
    /// never deleted -- that's the "with versioning" requirement: the full
    /// history of what a template looked like stays queryable.</summary>
    public bool IsActive { get; set; } = true;

    public Guid CreatedById { get; set; }
}
