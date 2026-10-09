using PhysioTrac.Application.Clinical;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Tests;

/// <summary>Server-side rules for template values: types, validation
/// rules, conditional fields and required fields at signing.</summary>
public class TemplateRulesTests
{
    private static readonly IReadOnlyList<TemplateFieldDto> Fields =
    [
        new("report", "Patient report", TemplateFieldType.LongText, IsRequired: true, NoteColumn: "subjective"),
        new("pain", "Pain", TemplateFieldType.PainScale, IsRequired: true, ScaleMin: 0, ScaleMax: 10),
        new("reps", "Reps", TemplateFieldType.Number, Validation: new FieldValidationDto(1, 50)),
        new("side", "Side", TemplateFieldType.Select, Options: ["Left", "Right"]),
        new("aids", "Aids", TemplateFieldType.Multiselect, Options: ["Cane", "Walker"]),
        new("events", "Adverse events", TemplateFieldType.Radio, IsRequired: true, Options: ["Yes", "No"]),
        new("eventDetail", "Event details", TemplateFieldType.LongText, IsRequired: true, Condition: new FieldConditionDto("events", "Yes")),
        new("mrn", "Code", TemplateFieldType.ShortText, Validation: new FieldValidationDto(Pattern: "^[A-Z]{2}-\\d+$", PatternMessage: "use AA-123.")),
        new("consent", "Consent given", TemplateFieldType.Checkbox, IsRequired: true),
        new("rows", "Exercises", TemplateFieldType.StructuredTable, Columns: [new("name", "Name")]),
    ];

    private static IReadOnlyDictionary<string, string?> Columns(string? report) => new Dictionary<string, string?> { ["subjective"] = report };

    [Fact]
    public void ValidValues_PassTheRules()
    {
        Assert.Empty(TemplateRules.ValidateValues(Fields,
        [
            new("pain", Number: 4), new("reps", Number: 12), new("side", Text: "Left"), new("aids", Json: "[\"Cane\"]"),
            new("mrn", Text: "AB-12"), new("rows", Json: "[{\"name\":\"Bridges\"}]"),
        ]));
    }

    [Theory]
    [InlineData("pain", 11, null, null)]
    [InlineData("reps", 0, null, null)]
    [InlineData("side", null, "Middle", null)]
    [InlineData("aids", null, null, "[\"Crutches\"]")]
    [InlineData("mrn", null, "12345", null)]
    [InlineData("rows", null, null, "{\"not\":\"a list\"}")]
    [InlineData("unknown", null, "x", null)]
    public void OutOfRuleValues_AreRejected(string key, int? number, string? text, string? json)
    {
        var errors = TemplateRules.ValidateValues(Fields, [new(key, Text: text, Number: number, Json: json)]);
        Assert.Single(errors);
    }

    [Fact]
    public void AFieldStoredInANoteColumn_IsNotSentAsAValue()
    {
        Assert.Single(TemplateRules.ValidateValues(Fields, [new("report", Text: "x")]));
    }

    [Fact]
    public void MissingRequired_ListsOnlyVisibleEmptyRequiredFields()
    {
        var missing = TemplateRules.MissingRequired(Fields, [new("events", Text: "No")], Columns(null));
        Assert.Equal(new[] { "Patient report", "Pain", "Consent given" }, missing.ToArray());

        var withYes = TemplateRules.MissingRequired(Fields,
            [new("events", Text: "Yes"), new("pain", Number: 0), new("consent", Bool: true)], Columns("Better today"));
        Assert.Equal(new[] { "Event details" }, withYes.ToArray()); // shown once "Yes" is chosen; pain 0 counts as filled
    }

    [Fact]
    public void ConditionsSupportAnyOfAndNotEmpty()
    {
        var anyOf = new TemplateFieldDto("x", "X", TemplateFieldType.ShortText, Condition: new FieldConditionDto("side", AnyOf: ["Left", "Both"]));
        var notEmpty = new TemplateFieldDto("y", "Y", TemplateFieldType.ShortText, Condition: new FieldConditionDto("side", NotEmpty: true));
        var values = new Dictionary<string, TemplateFieldValueDto>(StringComparer.OrdinalIgnoreCase) { ["side"] = new("side", Text: "left") };

        Assert.True(TemplateRules.IsVisible(anyOf, values));
        Assert.True(TemplateRules.IsVisible(notEmpty, values));
        Assert.False(TemplateRules.IsVisible(notEmpty, new Dictionary<string, TemplateFieldValueDto>()));
        var multi = new Dictionary<string, TemplateFieldValueDto> { ["side"] = new("side", Json: "[\"Both\"]") };
        Assert.True(TemplateRules.IsVisible(anyOf, multi));
    }
}
