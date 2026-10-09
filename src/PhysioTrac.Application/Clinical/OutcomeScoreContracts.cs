using System.Text.Json;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

public record OutcomeScoreDto(
    Guid Id, Guid PatientId, Guid? NoteId, Guid RecordedById, OutcomeMeasure Measure, DateOnly MeasuredOn, decimal Score,
    decimal? MaximumScore, string? Interpretation = null, IReadOnlyList<OutcomeItemResponseDto>? ItemResponses = null,
    string? Notes = null, bool IsLocked = false);

/// <summary>Records a score. With <see cref="ItemResponses"/> the server
/// scores them (the authoritative score); without, <see cref="Score"/> is
/// the total entered directly and only range-checked.</summary>
public record RecordOutcomeScoreRequest(
    Guid PatientId, Guid? NoteId, OutcomeMeasure Measure, DateOnly MeasuredOn, decimal? Score, decimal? MaximumScore, string? Notes,
    IReadOnlyList<OutcomeItemResponseDto>? ItemResponses = null);

public static class OutcomeScoreMapper
{
    public static OutcomeScoreDto ToDto(OutcomeScore o, bool locked) => new(
        o.Id, o.PatientId, o.NoteId, o.RecordedById, o.Measure, o.MeasuredOn, o.Score, o.MaximumScore,
        o.Interpretation ?? (OutcomeMeasureCatalog.IsKnown(o.Measure) ? OutcomeMeasureCatalog.Interpret(o.Measure, o.Score) : null),
        Responses(o.ItemResponsesJson), o.Notes, locked);

    /// <summary>The stored item responses, or null when only a total was entered.</summary>
    public static IReadOnlyList<OutcomeItemResponseDto>? Responses(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('[')) return null;
        try { return JsonSerializer.Deserialize<List<OutcomeItemResponseDto>>(json); }
        catch (JsonException) { return null; }
    }
}
