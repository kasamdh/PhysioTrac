using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

/// <summary>The note sections AI can draft. Subjective is the patient's own
/// report and objective data are measured, so neither is ever generated.</summary>
public enum AiDraftSection
{
    Assessment,
    Plan,
}

/// <summary>Which AI provider drafts documentation. "Mock" (the default)
/// is deterministic and sends nothing anywhere; "None" turns AI off. A real
/// provider plugs in behind <see cref="IDocumentationAiProvider"/> and must
/// be covered by a business associate agreement before it sees PHI.</summary>
public class DocumentationAiOptions
{
    public const string SectionName = "DocumentationAi";

    public string Provider { get; set; } = "Mock";
}

/// <summary>What a provider is given: the visit's structured clinical data
/// only (minimum necessary). Never the patient's name, date of birth, MRN,
/// address, contact details, clinician names, dates of service or the
/// free-text narrative.</summary>
public record AiDocumentationContext(
    NoteType NoteType,
    int VisitNumber,
    IReadOnlyList<string> Diagnoses,
    AiPainContext? Pain,
    IReadOnlyList<string> Measurements,
    IReadOnlyList<string> SpecialTests,
    IReadOnlyList<AiIntervention> Interventions,
    IReadOnlyList<AiGoalProgress> Goals,
    IReadOnlyList<string> Outcomes,
    int? FrequencyPerWeek,
    int? DurationWeeks);

/// <param name="ScaleMaximum">Top of the scale used ("10", "100" for VAS, "3" for verbal).</param>
public record AiPainContext(decimal? Current, decimal? Worst, decimal? BeforeTreatment, decimal? AfterTreatment, string? Location, string ScaleMaximum);

public record AiIntervention(string Description, int Minutes);

public record AiGoalProgress(GoalTerm Term, string Task, decimal Baseline, decimal? Current, decimal Target, string? Unit, GoalStatus Status, int? ProgressPercent);

/// <summary>A provider-independent drafting engine. It returns text only;
/// it has no access to the note, the database or signing.</summary>
public interface IDocumentationAiProvider
{
    /// <summary>Shown to the clinician with every suggestion (e.g. "Mock").</summary>
    string Name { get; }

    Task<string> DraftAsync(AiDraftSection section, AiDocumentationContext context, CancellationToken ct = default);
}

/// <summary>A suggestion for the clinician to review. Nothing is saved to
/// the note until the clinician inserts it and saves.</summary>
public record AiDraftDto(AiDraftSection Section, string Text, string Provider, DateTimeOffset GeneratedAt, string Notice);

public record AiStatusDto(bool Enabled, string Provider);

public interface IDocumentationAiService
{
    AiStatusDto GetStatus();

    /// <summary>Drafts a section for a note the caller may edit. Never
    /// changes the note.</summary>
    Task<AiDraftDto> DraftAsync(Guid noteId, AiDraftSection section, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Records that the clinician put an AI suggestion into the note
    /// (the note is then marked AI-assisted when it is signed).</summary>
    Task RecordInsertedAsync(Guid noteId, AiDraftSection section, ICurrentUser actor, CancellationToken ct = default);
}

public static class AiRules
{
    public const string DraftedAction = "note.ai_draft_requested";
    public const string InsertedAction = "note.ai_draft_inserted";

    public const string Notice =
        "AI-generated draft from this visit's structured data. Review, edit and confirm every statement before saving; " +
        "you remain responsible for the note. AI never signs or finalizes documentation and does not diagnose.";

    private const int MaxText = 120;

    private static string Clip(string s) => s.Length <= MaxText ? s : s[..MaxText].TrimEnd() + "…";

    private static string Number(decimal d) => d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Builds the de-identified context from an encounter.</summary>
    public static AiDocumentationContext ContextFrom(EncounterDto e)
    {
        var pain = e.Pain is { } p && (p.Current ?? p.Worst ?? p.BeforeTreatment ?? p.AfterTreatment) is not null
            ? new AiPainContext(p.Current, p.Worst, p.BeforeTreatment, p.AfterTreatment,
                string.IsNullOrWhiteSpace(p.Location) ? null : Clip(p.Location.Trim()),
                Number(PainRules.Range(p.Scale).Max))
            : null;

        var measurements = (e.Measurements ?? [])
            .Where(m => m.NumericValue is not null)
            .Select(m => Clip(string.Join(" ", new[]
            {
                m.Item, m.Movement, m.Mode, m.Side is { } s ? $"({s})" : null,
                $"{Number(m.NumericValue!.Value)}{(m.Unit is "deg" or "°" ? "°" : m.Unit is null ? "" : " " + m.Unit)}",
            }.Where(x => !string.IsNullOrWhiteSpace(x)))))
            .ToList();

        var tests = (e.SpecialTests ?? [])
            .Where(t => t.Outcome != SpecialTestOutcome.NotTested)
            .Select(t => Clip($"{t.TestName}{(t.Side is { } s ? $" ({s})" : "")}: {t.Outcome}"))
            .ToList();

        var interventions = (e.Flowsheet ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f.Description))
            .Select(f => new AiIntervention(Clip(f.Description.Trim()), f.Minutes))
            .ToList();

        var goals = (e.GoalProgress ?? [])
            .Where(g => !string.IsNullOrWhiteSpace(g.FunctionalTask))
            .Select(g => new AiGoalProgress(g.Term, Clip(g.FunctionalTask!.Trim()), g.BaselineValue, g.CurrentValue, g.TargetValue,
                g.Unit, g.Status, g.ProgressPercent))
            .ToList();

        var outcomes = (e.Outcomes ?? [])
            .Select(o => $"{o.Measure} {Number(o.Score)}{(o.MaximumScore is { } max ? $"/{Number(max)}" : "")}")
            .ToList();

        return new AiDocumentationContext(e.Note.NoteType, e.Header.VisitNumber, e.Header.Diagnoses.Select(Clip).ToList(), pain,
            measurements, tests, interventions, goals, outcomes, e.Note.FrequencyPerWeek, e.Note.DurationWeeks);
    }
}
