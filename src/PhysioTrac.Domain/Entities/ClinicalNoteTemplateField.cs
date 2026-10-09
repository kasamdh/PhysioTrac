using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>One configurable field in a template version.</summary>
public class ClinicalNoteTemplateField : BaseEntity
{
    public Guid VersionId { get; set; }
    public ClinicalNoteTemplateVersion? Version { get; set; }

    public Guid SectionId { get; set; }
    public ClinicalNoteTemplateSection? Section { get; set; }

    /// <summary>Stable key, unique within the version (e.g. "chiefComplaint").
    /// Values are stored per note under this key.</summary>
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public TemplateFieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
    public string? HelpText { get; set; }
    public string? Placeholder { get; set; }

    /// <summary>Unit for Number / ClinicalMeasurement fields (e.g. "deg", "sec").</summary>
    public string? Unit { get; set; }

    /// <summary>When set, the value is stored in that ClinicalNote column
    /// instead of a field-value row: "subjective", "objective",
    /// "interventions", "assessment" or "plan". Keeps the S/O/A/P narrative
    /// where reports, compliance checks and existing screens already read it.</summary>
    public string? NoteColumn { get; set; }

    /// <summary>Type-specific settings (genuinely dynamic, small JSON):
    /// {"options":["..."]} for Select/Radio/Multiselect,
    /// {"columns":[{"key","label"}]} for StructuredTable,
    /// {"min":0,"max":10} for PainScale.</summary>
    public string? ConfigJson { get; set; }

    /// <summary>Server-checked rules, small JSON: {"min":..,"max":..,"maxLength":..,"pattern":".."}.</summary>
    public string? ValidationJson { get; set; }

    /// <summary>Show this field only when another field in the same version
    /// matches, small JSON: {"field":"key","equals":"Yes"} or
    /// {"field":"key","in":["A","B"]} or {"field":"key","notEmpty":true}.
    /// A hidden field is never required.</summary>
    public string? ConditionJson { get; set; }
}
