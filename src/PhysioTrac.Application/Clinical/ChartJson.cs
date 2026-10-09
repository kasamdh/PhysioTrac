using System.Text.Json;

namespace PhysioTrac.Application.Clinical;

/// <summary>Validation for Clinical Charting's structured findings, stored in
/// ClinicalNote.SubjectiveDetailsJson / ObjectiveMeasurementsJson. The shape
/// inside is owned by the charting UI; the server only guarantees each value
/// is a JSON object of reasonable size, so a malformed or oversized payload
/// can never be stored in (or later break) a clinical record.</summary>
public static class ChartJson
{
    /// <summary>64 KB: far above a full exam (dozens of ROM/MMT rows), well
    /// below anything that would bloat the note's version history.</summary>
    public const int MaxLength = 64 * 1024;

    /// <summary>Null stays null ("unchanged"); otherwise returns the compact
    /// form, or throws InvalidOperationException naming the field.</summary>
    public static string? Normalize(string? json, string fieldName)
    {
        if (json is null) return null;
        if (json.Length > MaxLength)
        {
            throw new InvalidOperationException($"{fieldName} is too large (limit {MaxLength / 1024} KB).");
        }
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"{fieldName} must be a JSON object.");
            }
            return JsonSerializer.Serialize(doc.RootElement);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"{fieldName} is not valid JSON.");
        }
    }
}
