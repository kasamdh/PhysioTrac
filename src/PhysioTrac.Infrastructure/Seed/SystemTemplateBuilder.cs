using PhysioTrac.Application.Clinical;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Infrastructure.Seed;

/// <summary>A system template definition: stable key + content.</summary>
public sealed record SystemTemplate(string Key, string Name, NoteType NoteType, ClinicalSpecialty Specialty, string Description,
    IReadOnlyList<TemplateSectionDto> Sections);

/// <summary>Compact helpers for writing system template definitions.</summary>
internal static class Tpl
{
    public static TemplateSectionDto Section(string key, string title, params TemplateFieldDto[] fields) =>
        new(key, title, fields);

    public static TemplateSectionDto Section(string key, string title, string? component, string? help, params TemplateFieldDto[] fields) =>
        new(key, title, fields, help, component);

    public static TemplateFieldDto Short(string key, string label) => new(key, label, TemplateFieldType.ShortText);
    public static TemplateFieldDto Long(string key, string label, string? noteColumn = null) =>
        new(key, label, TemplateFieldType.LongText, NoteColumn: noteColumn);
    public static TemplateFieldDto Number(string key, string label, string? unit = null, decimal? min = null, decimal? max = null) =>
        new(key, label, TemplateFieldType.Number, Unit: unit, Validation: min is null && max is null ? null : new FieldValidationDto(min, max));
    public static TemplateFieldDto Measure(string key, string label, string unit, decimal? min = null, decimal? max = null) =>
        new(key, label, TemplateFieldType.ClinicalMeasurement, Unit: unit, Validation: min is null && max is null ? null : new FieldValidationDto(min, max));
    public static TemplateFieldDto Date(string key, string label) => new(key, label, TemplateFieldType.Date);
    public static TemplateFieldDto Time(string key, string label) => new(key, label, TemplateFieldType.Time);
    public static TemplateFieldDto Check(string key, string label) => new(key, label, TemplateFieldType.Checkbox);
    public static TemplateFieldDto Radio(string key, string label, params string[] options) =>
        new(key, label, TemplateFieldType.Radio, Options: options);
    public static TemplateFieldDto Select(string key, string label, params string[] options) =>
        new(key, label, TemplateFieldType.Select, Options: options);
    public static TemplateFieldDto Multi(string key, string label, params string[] options) =>
        new(key, label, TemplateFieldType.Multiselect, Options: options);
    public static TemplateFieldDto Pain(string key, string label) =>
        new(key, label, TemplateFieldType.PainScale, ScaleMin: 0, ScaleMax: 10);
    public static TemplateFieldDto Table(string key, string label, params (string Key, string Label)[] columns) =>
        new(key, label, TemplateFieldType.StructuredTable, Columns: columns.Select(c => new TableColumnDto(c.Key, c.Label)).ToList());
    public static TemplateFieldDto Signature() => new("signature", "Therapist signature", TemplateFieldType.Signature);

    public static TemplateFieldDto Req(this TemplateFieldDto f) => f with { IsRequired = true };
    public static TemplateFieldDto Help(this TemplateFieldDto f, string help) => f with { HelpText = help };
    public static TemplateFieldDto Hint(this TemplateFieldDto f, string placeholder) => f with { Placeholder = placeholder };
    public static TemplateFieldDto When(this TemplateFieldDto f, string field, string value) => f with { Condition = new FieldConditionDto(field, value) };
    public static TemplateFieldDto WhenFilled(this TemplateFieldDto f, string field) => f with { Condition = new FieldConditionDto(field, NotEmpty: true) };

    public static readonly string[] YesNo = ["Yes", "No"];
    public static readonly string[] AssistanceLevels =
        ["Independent", "Modified independent", "Supervision", "Contact guard", "Minimal assist", "Moderate assist", "Maximal assist", "Dependent"];
    public static readonly string[] Prognoses = ["Excellent", "Good", "Fair", "Poor", "Guarded"];
}
