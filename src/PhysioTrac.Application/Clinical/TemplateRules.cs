using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

/// <summary>The rules for documentation templates, in one place: what makes
/// a template definition valid, when a conditional field is shown, and
/// whether a note's values satisfy the fields (types, validation rules,
/// required fields). Pure functions -- the React form mirrors them for
/// instant feedback, but the server's answer is the one that counts.</summary>
public static partial class TemplateRules
{
    /// <summary>ClinicalNote narrative columns a text field may write to.</summary>
    public static readonly IReadOnlySet<string> NoteColumns = new HashSet<string>
    {
        "subjective", "objective", "interventions", "assessment", "plan",
    };

    /// <summary>Built-in clinical components a section may embed.</summary>
    public static readonly IReadOnlySet<string> Components = new HashSet<string>
    {
        "painAssessment", "bodyChart", "measurements", "specialTests", "outcomes", "interventions", "goals",
        "planOfCare", "diagnoses", "medicalHistory", "carryForward",
    };

    private static readonly HashSet<TemplateFieldType> OptionTypes =
        [TemplateFieldType.Select, TemplateFieldType.Radio, TemplateFieldType.Multiselect];

    private static readonly HashSet<TemplateFieldType> TextTypes =
        [TemplateFieldType.ShortText, TemplateFieldType.LongText];

    [GeneratedRegex("^[a-zA-Z][a-zA-Z0-9_-]{0,79}$")]
    private static partial Regex KeyPattern();

    // ---------------------------------------------------------------- definition

    /// <summary>Every problem with a template definition (empty = valid).</summary>
    public static IReadOnlyList<string> ValidateDefinition(string name, IReadOnlyList<TemplateSectionDto> sections)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name)) errors.Add("Enter a template name.");
        else if (name.Trim().Length > 200) errors.Add("The template name is too long (200 characters at most).");
        if (sections.Count == 0) errors.Add("Add at least one section.");

        var sectionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fieldKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedColumns = new HashSet<string>();
        var signatureFields = 0;
        var allFields = sections.SelectMany(s => s.Fields).ToList();

        foreach (var section in sections)
        {
            var where = $"Section \"{(string.IsNullOrWhiteSpace(section.Title) ? section.Key : section.Title)}\"";
            if (!KeyPattern().IsMatch(section.Key ?? "")) errors.Add($"{where}: the key must start with a letter and use only letters, numbers, - or _.");
            else if (!sectionKeys.Add(section.Key)) errors.Add($"{where}: the key \"{section.Key}\" is used twice.");
            if (string.IsNullOrWhiteSpace(section.Title)) errors.Add($"{where}: enter a title.");
            if (section.Component is not null && !Components.Contains(section.Component))
                errors.Add($"{where}: unknown component \"{section.Component}\".");
            if (section.Fields.Count == 0 && section.Component is null)
                errors.Add($"{where}: add a field or a clinical component.");

            foreach (var f in section.Fields)
            {
                var fw = $"Field \"{(string.IsNullOrWhiteSpace(f.Label) ? f.Key : f.Label)}\"";
                if (!KeyPattern().IsMatch(f.Key ?? "")) errors.Add($"{fw}: the key must start with a letter and use only letters, numbers, - or _.");
                else if (!fieldKeys.Add(f.Key)) errors.Add($"{fw}: the key \"{f.Key}\" is used twice in this template.");
                if (string.IsNullOrWhiteSpace(f.Label)) errors.Add($"{fw}: enter a label.");
                if ((f.Label?.Length ?? 0) > 200 || (f.HelpText?.Length ?? 0) > 1000) errors.Add($"{fw}: the label or help text is too long.");

                if (OptionTypes.Contains(f.FieldType))
                {
                    var options = f.Options ?? [];
                    if (options.Count == 0 || options.Any(string.IsNullOrWhiteSpace)) errors.Add($"{fw}: add the choices.");
                    else if (options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count) errors.Add($"{fw}: a choice is listed twice.");
                }
                if (f.FieldType == TemplateFieldType.StructuredTable)
                {
                    var columns = f.Columns ?? [];
                    if (columns.Count == 0) errors.Add($"{fw}: add the table columns.");
                    else if (columns.Any(c => !KeyPattern().IsMatch(c.Key ?? "") || string.IsNullOrWhiteSpace(c.Label)))
                        errors.Add($"{fw}: every column needs a key and a label.");
                    else if (columns.Select(c => c.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Count)
                        errors.Add($"{fw}: a column key is used twice.");
                }
                if (f.FieldType == TemplateFieldType.PainScale && f.ScaleMin is decimal lo && f.ScaleMax is decimal hi && lo >= hi)
                    errors.Add($"{fw}: the scale minimum must be below its maximum.");
                if (f.FieldType == TemplateFieldType.Signature)
                {
                    signatureFields++;
                    if (f.IsRequired) errors.Add($"{fw}: a signature is completed by signing, so it can't be a required field.");
                }

                if (f.NoteColumn is not null)
                {
                    if (!NoteColumns.Contains(f.NoteColumn)) errors.Add($"{fw}: unknown note column \"{f.NoteColumn}\".");
                    else if (!TextTypes.Contains(f.FieldType)) errors.Add($"{fw}: only text fields can be stored in the note's {f.NoteColumn}.");
                    else if (!usedColumns.Add(f.NoteColumn)) errors.Add($"{fw}: another field already uses the note's {f.NoteColumn}.");
                }

                if (f.Validation is { } v)
                {
                    if (v.Min is decimal min && v.Max is decimal max && min > max) errors.Add($"{fw}: the minimum is greater than the maximum.");
                    if (v.MaxLength is < 1 or > 20000) errors.Add($"{fw}: the maximum length must be 1 to 20000.");
                    if (!string.IsNullOrEmpty(v.Pattern))
                    {
                        try { _ = new Regex(v.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100)); }
                        catch (ArgumentException) { errors.Add($"{fw}: the pattern is not a valid regular expression."); }
                    }
                }

                if (f.Condition is { } c)
                {
                    var target = allFields.FirstOrDefault(x => string.Equals(x.Key, c.Field, StringComparison.OrdinalIgnoreCase));
                    if (target is null) errors.Add($"{fw}: it depends on a field (\"{c.Field}\") that isn't in this template.");
                    else if (ReferenceEquals(target, f) || string.Equals(target.Key, f.Key, StringComparison.OrdinalIgnoreCase))
                        errors.Add($"{fw}: a field can't depend on itself.");
                    if (c.Value is null && (c.AnyOf is null || c.AnyOf.Count == 0) && c.NotEmpty is not true)
                        errors.Add($"{fw}: say which answer shows it.");
                }
            }
        }
        if (signatureFields > 1) errors.Add("A template can have only one signature field.");
        return errors;
    }

    // ---------------------------------------------------------------- values

    /// <summary>The value as text, for conditions: "true"/"false" for a
    /// checkbox, invariant numbers, ISO dates.</summary>
    public static string? AsText(TemplateFieldValueDto? v)
    {
        if (v is null) return null;
        if (!string.IsNullOrWhiteSpace(v.Text)) return v.Text;
        if (v.Number is decimal n) return n.ToString(CultureInfo.InvariantCulture);
        if (v.Bool is bool b) return b ? "true" : "false";
        if (v.Date is DateOnly d) return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (v.Time is TimeOnly t) return t.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(v.Json) && v.Json is not "[]" and not "null") return v.Json;
        return null;
    }

    private static IReadOnlyList<string> AsList(TemplateFieldValueDto? v)
    {
        if (v?.Json is null) return AsText(v) is string s ? [s] : [];
        try
        {
            using var doc = JsonDocument.Parse(v.Json);
            return doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
                : [];
        }
        catch (JsonException) { return []; }
    }

    /// <summary>Whether a field is shown given the other values (a hidden
    /// field is never required and its value is ignored).</summary>
    public static bool IsVisible(TemplateFieldDto field, IReadOnlyDictionary<string, TemplateFieldValueDto> values)
    {
        if (field.Condition is not { } c) return true;
        values.TryGetValue(c.Field, out var target);
        var list = AsList(target);
        if (c.NotEmpty is true && list.Count == 0) return false;
        if (c.Value is not null && !list.Contains(c.Value, StringComparer.OrdinalIgnoreCase)) return false;
        if (c.AnyOf is { Count: > 0 } any && !list.Any(x => any.Contains(x, StringComparer.OrdinalIgnoreCase))) return false;
        return true;
    }

    /// <summary>Type and rule problems with the values being saved (drafts
    /// included). Required fields are checked only at signing -- see
    /// <see cref="MissingRequired"/>.</summary>
    public static IReadOnlyList<string> ValidateValues(IReadOnlyList<TemplateFieldDto> fields, IReadOnlyList<TemplateFieldValueDto> values)
    {
        var errors = new List<string>();
        var byKey = fields.ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in values)
        {
            if (!byKey.TryGetValue(v.Key, out var f)) { errors.Add($"\"{v.Key}\" is not a field of this note's template."); continue; }
            if (!seen.Add(v.Key)) { errors.Add($"{f.Label}: sent twice."); continue; }
            if (f.NoteColumn is not null) { errors.Add($"{f.Label}: saved with the note's {f.NoteColumn}, not as a field value."); continue; }
            errors.AddRange(ValidateOne(f, v).Select(e => $"{f.Label}: {e}"));
        }
        return errors;
    }

    private static IEnumerable<string> ValidateOne(TemplateFieldDto f, TemplateFieldValueDto v)
    {
        var rules = f.Validation;
        switch (f.FieldType)
        {
            case TemplateFieldType.ShortText:
            case TemplateFieldType.LongText:
                var text = v.Text ?? "";
                var maxLength = rules?.MaxLength ?? (f.FieldType == TemplateFieldType.ShortText ? 500 : 20000);
                if (text.Length > maxLength) yield return $"{maxLength} characters at most.";
                if (!string.IsNullOrEmpty(rules?.Pattern) && text.Length > 0 &&
                    !Regex.IsMatch(text, rules.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100)))
                    yield return rules.PatternMessage ?? "not in the expected format.";
                break;
            case TemplateFieldType.Number:
            case TemplateFieldType.ClinicalMeasurement:
            case TemplateFieldType.PainScale:
                if (v.Number is decimal n)
                {
                    var min = f.FieldType == TemplateFieldType.PainScale ? f.ScaleMin ?? 0 : rules?.Min;
                    var max = f.FieldType == TemplateFieldType.PainScale ? f.ScaleMax ?? 10 : rules?.Max;
                    if (min is decimal lo && n < lo) yield return $"must be at least {lo.ToString(CultureInfo.InvariantCulture)}.";
                    if (max is decimal hi && n > hi) yield return $"must be at most {hi.ToString(CultureInfo.InvariantCulture)}.";
                }
                else if (!string.IsNullOrWhiteSpace(v.Text)) yield return "enter a number.";
                break;
            case TemplateFieldType.Select:
            case TemplateFieldType.Radio:
                if (!string.IsNullOrWhiteSpace(v.Text) && !(f.Options ?? []).Contains(v.Text)) yield return $"\"{v.Text}\" is not one of the choices.";
                break;
            case TemplateFieldType.Multiselect:
                if (v.Json is not null)
                {
                    var picked = AsList(v);
                    if (picked.Count == 0 && v.Json.Trim() is not "[]") yield return "send the choices as a list.";
                    foreach (var p in picked.Where(p => !(f.Options ?? []).Contains(p))) yield return $"\"{p}\" is not one of the choices.";
                }
                break;
            case TemplateFieldType.StructuredTable:
                if (v.Json is not null)
                {
                    string? problem = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(v.Json);
                        if (doc.RootElement.ValueKind != JsonValueKind.Array) problem = "send the rows as a list.";
                        else if (doc.RootElement.GetArrayLength() > 200) problem = "200 rows at most.";
                        else if (v.Json.Length > 64 * 1024) problem = "the table is too large.";
                    }
                    catch (JsonException) { problem = "the table data is not valid."; }
                    if (problem is not null) yield return problem;
                }
                break;
            case TemplateFieldType.Signature:
                yield return "is completed by signing the note.";
                break;
        }
    }

    /// <summary>Labels of required, visible fields that have no value.
    /// <paramref name="noteColumns"/> holds the note's narrative text by
    /// column name, for fields stored there.</summary>
    public static IReadOnlyList<string> MissingRequired(
        IReadOnlyList<TemplateFieldDto> fields, IReadOnlyList<TemplateFieldValueDto> values, IReadOnlyDictionary<string, string?> noteColumns)
    {
        var byKey = values.GroupBy(v => v.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        foreach (var f in fields.Where(f => f.IsRequired && f.FieldType != TemplateFieldType.Signature))
        {
            if (!IsVisible(f, byKey)) continue;
            var filled = f.NoteColumn is not null
                ? !string.IsNullOrWhiteSpace(noteColumns.GetValueOrDefault(f.NoteColumn))
                : f.FieldType switch
                {
                    TemplateFieldType.Checkbox => byKey.GetValueOrDefault(f.Key)?.Bool is true,
                    TemplateFieldType.Multiselect or TemplateFieldType.StructuredTable => AsList(byKey.GetValueOrDefault(f.Key)).Count > 0
                        || HasRows(byKey.GetValueOrDefault(f.Key)?.Json),
                    _ => AsText(byKey.GetValueOrDefault(f.Key)) is not null,
                };
            if (!filled) missing.Add(f.Label);
        }
        return missing;
    }

    private static bool HasRows(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0;
        }
        catch (JsonException) { return false; }
    }
}
