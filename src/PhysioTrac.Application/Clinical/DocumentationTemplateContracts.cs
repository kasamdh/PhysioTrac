using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

public record TableColumnDto(string Key, string Label);

/// <summary>Server-checked rules for a field's value.</summary>
public record FieldValidationDto(decimal? Min = null, decimal? Max = null, int? MaxLength = null, string? Pattern = null, string? PatternMessage = null);

/// <summary>Show the field only when another field (same template version)
/// equals <see cref="Value"/>, is one of <see cref="AnyOf"/>, or is filled
/// in (<see cref="NotEmpty"/>).</summary>
public record FieldConditionDto(string Field, string? Value = null, IReadOnlyList<string>? AnyOf = null, bool? NotEmpty = null);

/// <summary>One template field, as defined (Id is set when read back; ignored on save).</summary>
public record TemplateFieldDto(
    string Key,
    string Label,
    TemplateFieldType FieldType,
    bool IsRequired = false,
    string? HelpText = null,
    string? Placeholder = null,
    string? Unit = null,
    string? NoteColumn = null,
    IReadOnlyList<string>? Options = null,
    IReadOnlyList<TableColumnDto>? Columns = null,
    decimal? ScaleMin = null,
    decimal? ScaleMax = null,
    FieldValidationDto? Validation = null,
    FieldConditionDto? Condition = null,
    Guid? Id = null,
    int DisplayOrder = 0);

public record TemplateSectionDto(
    string Key,
    string Title,
    IReadOnlyList<TemplateFieldDto> Fields,
    string? HelpText = null,
    string? Component = null,
    int DisplayOrder = 0);

/// <summary>A complete, immutable template version.</summary>
public record TemplateVersionDto(
    Guid Id, Guid TemplateId, int VersionNumber, string? ChangeSummary, DateTimeOffset CreatedAt, string? CreatedByName,
    IReadOnlyList<TemplateSectionDto> Sections);

public record TemplateVersionSummaryDto(Guid Id, int VersionNumber, string? ChangeSummary, DateTimeOffset CreatedAt, string? CreatedByName, int NotesUsing);

public record DocumentationTemplateDto(
    Guid Id, string Name, NoteType NoteType, ClinicalSpecialty Specialty, string? Description,
    bool IsSystem, bool IsActive, Guid CurrentVersionId, int CurrentVersionNumber,
    IReadOnlyList<Guid> AppointmentTypeIds, bool IsFavorite, DateTimeOffset UpdatedAt);

public record DocumentationTemplateDetailDto(DocumentationTemplateDto Template, TemplateVersionDto CurrentVersion);

/// <summary>Create a template, or save an edit. Any change to the sections
/// or fields publishes a new version; existing notes keep theirs.</summary>
public record SaveDocumentationTemplateRequest(
    string Name,
    NoteType NoteType,
    ClinicalSpecialty Specialty,
    IReadOnlyList<TemplateSectionDto> Sections,
    string? Description = null,
    IReadOnlyList<Guid>? AppointmentTypeIds = null,
    string? ChangeSummary = null);

public record SetTemplateActiveRequest(bool IsActive);

public record CopyTemplateRequest(string? Name = null);

/// <summary>One value of a template field on a note. Use the member that
/// matches the field's type; Json only for Multiselect (array of strings)
/// and StructuredTable (array of row objects).</summary>
public record TemplateFieldValueDto(
    string Key, string? Text = null, decimal? Number = null, DateOnly? Date = null, TimeOnly? Time = null, bool? Bool = null, string? Json = null);

/// <summary>A template definition or a set of values broke the rules; each
/// message names the section/field it is about.</summary>
public class TemplateValidationException : InvalidOperationException
{
    public IReadOnlyList<string> Errors { get; }

    public TemplateValidationException(IReadOnlyList<string> errors)
        : base(errors.Count == 1 ? errors[0] : $"{errors.Count} problems: {string.Join(" ", errors)}")
    {
        Errors = errors;
    }
}
