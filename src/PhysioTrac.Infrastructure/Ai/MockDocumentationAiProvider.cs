using System.Globalization;
using System.Text;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Infrastructure.Ai;

/// <summary>Development and test provider: builds the draft from the
/// structured context with fixed sentences, so the same visit always gives
/// the same text and nothing leaves the server. Where clinical judgment is
/// needed it leaves a [bracketed prompt] for the clinician instead of
/// inventing one.</summary>
public class MockDocumentationAiProvider : IDocumentationAiProvider
{
    public string Name => "Mock";

    public Task<string> DraftAsync(AiDraftSection section, AiDocumentationContext c, CancellationToken ct = default) =>
        Task.FromResult(section switch
        {
            AiDraftSection.Assessment => Assessment(c),
            AiDraftSection.Plan => Plan(c),
            _ => throw new InvalidOperationException("Unknown section."),
        });

    private static string N(decimal d) => d.ToString("0.##", CultureInfo.InvariantCulture);

    private static string VisitName(NoteType t) => t switch
    {
        NoteType.Evaluation or NoteType.PelvicHealthEvaluation => "initial evaluation",
        NoteType.Progress => "progress visit",
        NoteType.ReEvaluation => "re-evaluation",
        NoteType.Recertification => "recertification visit",
        NoteType.Discharge => "discharge visit",
        _ => "treatment visit",
    };

    private static string Assessment(AiDocumentationContext c)
    {
        var s = new StringBuilder();
        s.Append($"Patient seen for {VisitName(c.NoteType)} (visit {c.VisitNumber})");
        s.Append(c.Diagnoses.Count > 0 ? $" for {string.Join("; ", c.Diagnoses)}. " : ". ");

        if (c.Pain is { } p)
        {
            var where = p.Location is null ? "" : $" ({p.Location})";
            if (p.BeforeTreatment is { } before && p.AfterTreatment is { } after)
            {
                var trend = after < before ? "decreased" : after > before ? "increased" : "was unchanged";
                s.Append($"Pain{where} {trend} from {N(before)}/{p.ScaleMaximum} before treatment to {N(after)}/{p.ScaleMaximum} after treatment. ");
            }
            else if (p.Current is { } now)
            {
                s.Append($"Pain{where} rated {N(now)}/{p.ScaleMaximum} today");
                s.Append(p.Worst is { } worst ? $", {N(worst)}/{p.ScaleMaximum} at worst. " : ". ");
            }
        }

        if (c.Measurements.Count > 0) s.Append($"Objective findings: {string.Join("; ", c.Measurements)}. ");
        if (c.SpecialTests.Count > 0) s.Append($"Special tests: {string.Join("; ", c.SpecialTests)}. ");
        if (c.Outcomes.Count > 0) s.Append($"Outcome measures: {string.Join("; ", c.Outcomes)}. ");

        if (c.Interventions.Count > 0)
        {
            var list = c.Interventions.Select(i => i.Minutes > 0 ? $"{i.Description} ({i.Minutes} min)" : i.Description);
            s.Append($"Treatment today included {string.Join(", ", list)}. ");
        }

        var goals = c.Goals.Where(g => g.Current is not null).ToList();
        if (goals.Count > 0)
        {
            s.Append("Goal progress: ");
            s.Append(string.Join("; ", goals.Select(g =>
                $"{(g.Term == GoalTerm.LongTerm ? "LTG" : "STG")} {g.Task} — {N(g.Current!.Value)} {g.Unit} (baseline {N(g.Baseline)}, target {N(g.Target)}" +
                (g.ProgressPercent is { } pct ? $", {pct}% toward goal)" : ")") +
                (g.Status == GoalStatus.Met ? ", met" : ""))));
            s.Append(". ");
        }

        s.Append("[Clinician: state the patient's response to treatment, remaining impairments and functional limitations, " +
                 "and why continued skilled therapy is or is not needed.]");
        return s.ToString().Trim();
    }

    private static string Plan(AiDocumentationContext c)
    {
        if (c.NoteType == NoteType.Discharge)
            return "Discharge from skilled physical therapy. Patient to continue the home exercise program independently. " +
                   "[Clinician: state the discharge reason, goal status at discharge and any follow-up or referrals.]";

        var s = new StringBuilder();
        s.Append(c.FrequencyPerWeek is { } f && c.DurationWeeks is { } w
            ? $"Continue skilled physical therapy {f}x/week for {w} weeks per the plan of care. "
            : "Continue skilled physical therapy per the plan of care. ");

        if (c.Interventions.Count > 0)
            s.Append($"Next visit: continue and progress {string.Join(", ", c.Interventions.Select(i => i.Description).Distinct().Take(4))} as tolerated. ");

        var open = c.Goals.Where(g => g.Status is not (GoalStatus.Met or GoalStatus.Discontinued)).ToList();
        if (open.Count > 0) s.Append($"Continue to address {open.Count} open goal{(open.Count == 1 ? "" : "s")}. ");

        s.Append("Review and update the home exercise program. ");
        s.Append("[Clinician: confirm the frequency, any changes to the plan, and the next reassessment.]");
        return s.ToString().Trim();
    }
}
