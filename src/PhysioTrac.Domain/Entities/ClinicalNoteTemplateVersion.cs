using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>One immutable version of a documentation template: its sections
/// and fields as they were when published. Editing a template publishes a
/// NEW version; a note records the exact version it was written with
/// (ClinicalNote.TemplateVersionId), so template changes never alter
/// existing documentation. Versions, sections and fields are append-only
/// (enforced in PhysioTracDbContext).</summary>
public class ClinicalNoteTemplateVersion : BaseEntity
{
    public Guid TemplateId { get; set; }
    public ClinicalNoteTemplate? Template { get; set; }

    /// <summary>1, 2, 3... per template.</summary>
    public int VersionNumber { get; set; }

    /// <summary>What changed in this version (optional, shown in history).</summary>
    public string? ChangeSummary { get; set; }

    public Guid? CreatedById { get; set; }

    public ICollection<ClinicalNoteTemplateSection> Sections { get; set; } = new List<ClinicalNoteTemplateSection>();
    public ICollection<ClinicalNoteTemplateField> Fields { get; set; } = new List<ClinicalNoteTemplateField>();
}

/// <summary>A titled group of fields in a template version (e.g. "Subjective").</summary>
public class ClinicalNoteTemplateSection : BaseEntity
{
    public Guid VersionId { get; set; }
    public ClinicalNoteTemplateVersion? Version { get; set; }

    /// <summary>Stable key within the version, e.g. "subjective".</summary>
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? HelpText { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>Optional built-in clinical component rendered in this section
    /// in addition to its fields (e.g. "interventions", "goals", "outcomes",
    /// "bodyChart") -- the data lives in that component's own structured
    /// tables, never in the template.</summary>
    public string? Component { get; set; }

    public ICollection<ClinicalNoteTemplateField> Fields { get; set; } = new List<ClinicalNoteTemplateField>();
}
