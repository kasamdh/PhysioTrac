using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Progress notes, re-evaluations, recertifications and discharge
/// summaries: an episode summary built only from signed charting, pre-filling
/// a note's empty fields from it, and the therapist's review of what was
/// pre-filled (required before signing).</summary>
public partial class ClinicalNoteService
{
    private static readonly string[] Verbal = ["none", "mild", "moderate", "severe"];

    public async Task<EpisodeSummaryDto> GetEpisodeSummaryAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await GetAsync(noteId, actor, ct);
        return await BuildEpisodeSummaryAsync(note, ct);
    }

    private sealed record Reading(string Key, string Label, DateOnly Date, DateTimeOffset Created, string Value);

    private async Task<EpisodeSummaryDto> BuildEpisodeSummaryAsync(ClinicalNote note, CancellationToken ct)
    {
        var notes = await SignedNotesOf(note).Where(n => n.ServiceDate <= note.ServiceDate)
            .Select(n => new { n.Id, n.NoteType, n.ServiceDate, n.CreatedAt }).ToListAsync(ct);
        bool Before(DateOnly d, DateTimeOffset c) => d < note.ServiceDate || (d == note.ServiceDate && c < note.CreatedAt);

        var plan = await _db.PlansOfCare.AsNoTracking()
            .Where(p => p.PatientId == note.PatientId && p.Status == PlanOfCareStatus.Active)
            .OrderByDescending(p => p.StartDate).FirstOrDefaultAsync(ct);
        var evaluation = notes.Where(n => n.NoteType is NoteType.Evaluation or NoteType.PelvicHealthEvaluation && Before(n.ServiceDate, n.CreatedAt))
            .OrderByDescending(n => n.ServiceDate).ThenByDescending(n => n.CreatedAt).FirstOrDefault();
        var episodeStart = evaluation?.ServiceDate ?? plan?.StartDate ?? notes.Select(n => (DateOnly?)n.ServiceDate).Min() ?? note.ServiceDate;
        var episode = notes.Where(n => n.ServiceDate >= episodeStart && Before(n.ServiceDate, n.CreatedAt)).ToList();
        var previousProgress = episode.Where(n => n.NoteType == NoteType.Progress)
            .OrderByDescending(n => n.ServiceDate).ThenByDescending(n => n.CreatedAt).FirstOrDefault();

        var periodStart = note.NoteType == NoteType.Progress && previousProgress is not null
            ? previousProgress.ServiceDate.AddDays(1) : episodeStart;
        var periodEnd = note.ServiceDate;
        var basis = note.NoteType == NoteType.Progress && previousProgress is not null
            ? $"since the progress note of {D(previousProgress.ServiceDate)}"
            : evaluation is not null ? $"since the evaluation of {D(evaluation.ServiceDate)}" : $"since {D(episodeStart)}";

        var ids = episode.Select(n => n.Id).ToList();
        var dates = episode.ToDictionary(n => n.Id, n => (n.ServiceDate, n.CreatedAt));
        var visitsInEpisode = episode.Count(n => EpisodeRules.IsVisit(n.NoteType));
        var visitsInPeriod = episode.Count(n => EpisodeRules.IsVisit(n.NoteType) && n.ServiceDate >= periodStart);

        // Attendance over the period, from the schedule.
        var from = periodStart.ToDateTime(TimeOnly.MinValue).AddDays(-1);
        var appointments = (await _db.Appointments.AsNoTracking()
                .Where(a => a.PatientId == note.PatientId && a.StartsAt >= from)
                .Select(a => new { a.StartsAt, a.Status }).ToListAsync(ct))
            .Where(a => DateOnly.FromDateTime(a.StartsAt.Date) >= periodStart && DateOnly.FromDateTime(a.StartsAt.Date) <= periodEnd)
            .ToList();
        var attended = appointments.Count(a => a.Status is AppointmentStatus.Completed or AppointmentStatus.CheckedIn or AppointmentStatus.InProgress);
        var cancelled = appointments.Count(a => a.Status == AppointmentStatus.Cancelled);
        var noShows = appointments.Count(a => a.Status == AppointmentStatus.NoShow);
        var attendance = new AttendanceDto(attended, cancelled, noShows, attended + cancelled + noShows == 0
            ? "No scheduled visits on record for this period."
            : $"{attended} of {attended + cancelled + noShows} scheduled visits attended ({noShows} no-show{(noShows == 1 ? "" : "s")}, {cancelled} cancellation{(cancelled == 1 ? "" : "s")}).");

        // Pain, measurements and outcome scores from signed notes only.
        var pain = (await _db.PainAssessments.AsNoTracking().Where(p => ids.Contains(p.NoteId)).ToListAsync(ct))
            .Where(p => p.Current is not null)
            .OrderBy(p => dates[p.NoteId].ServiceDate).ThenBy(p => dates[p.NoteId].CreatedAt).ToList();
        var painReadings = pain.Select(p => new Reading("pain", "Pain now", dates[p.NoteId].ServiceDate, dates[p.NoteId].CreatedAt,
            Rating(p.Scale, p.Current!.Value))).ToList();

        var measurements = (await _db.ObjectiveMeasurements.AsNoTracking().Where(m => ids.Contains(m.NoteId)).ToListAsync(ct))
            .Where(m => m.NumericValue is not null || !string.IsNullOrWhiteSpace(m.TextValue))
            .Select(m => new Reading(MeasurementRules.Key(m.Category, m.Item, m.Movement, m.Side, m.Mode, m.Unit), MeasurementLabel(m),
                dates[m.NoteId].ServiceDate, dates[m.NoteId].CreatedAt,
                m.NumericValue is decimal v ? $"{Num(v)}{(string.IsNullOrWhiteSpace(m.Unit) ? "" : " " + m.Unit)}" : m.TextValue!.Trim()))
            .ToList();

        var scores = (await _db.OutcomeScores.AsNoTracking()
                .Where(o => o.PatientId == note.PatientId && o.MeasuredOn >= episodeStart && o.MeasuredOn <= note.ServiceDate &&
                    (o.NoteId == null || ids.Contains(o.NoteId.Value)))
                .ToListAsync(ct))
            .Where(o => OutcomeMeasureCatalog.IsKnown(o.Measure))
            .OrderBy(o => o.MeasuredOn).ToList();

        var outcomeLines = OutcomeLines(scores, null);
        var goals = await _db.FunctionalGoals.AsNoTracking()
            .Where(g => g.PatientId == note.PatientId && g.Status != GoalStatus.Draft &&
                ((g.Status == GoalStatus.NotStarted || g.Status == GoalStatus.Active) || (plan != null && g.PlanOfCareId == plan.Id)))
            .OrderBy(g => g.Term).ThenBy(g => g.TargetDate).ToListAsync(ct);
        var goalLines = goals.Select(GoalLine).ToList();

        IReadOnlyList<string> Since(DateOnly? cutoff) => cutoff is null ? [] :
        [
            .. ChangeLines(painReadings, cutoff),
            .. ChangeLines(measurements, cutoff),
            .. OutcomeLines(scores, cutoff),
        ];

        // The home program: the plan's, plus home-exercise entries of the last signed visit that had any.
        var hepNoteIds = await _db.NoteInterventions.AsNoTracking()
            .Where(i => ids.Contains(i.NoteId) && i.Category == InterventionCategory.HomeExerciseProgram)
            .Select(i => i.NoteId).Distinct().ToListAsync(ct);
        var hepNote = episode.Where(n => hepNoteIds.Contains(n.Id))
            .OrderByDescending(n => n.ServiceDate).ThenByDescending(n => n.CreatedAt).FirstOrDefault();
        var hep = hepNote is null ? [] : (await _db.NoteInterventions.AsNoTracking()
                .Where(i => i.NoteId == hepNote.Id && i.Category == InterventionCategory.HomeExerciseProgram)
                .OrderBy(i => i.Order).ToListAsync(ct))
            .Select(i => string.Join(" ", new[] { i.Description, Dose(i) }.Where(s => !string.IsNullOrWhiteSpace(s)))).ToList();
        var homeProgram = string.Join("\n", new[] { plan?.HomeProgram, hep.Count > 0 ? "Home exercises: " + string.Join("; ", hep) + "." : null }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

        return new EpisodeSummaryDto(note.NoteType, episodeStart, periodStart, periodEnd, basis,
            plan?.Id, plan?.StartDate, plan?.EndDate, plan?.FrequencyPerWeek, plan?.DurationWeeks,
            visitsInPeriod, visitsInEpisode, attendance,
            ChangeLines(painReadings, null).FirstOrDefault(),
            ChangeLines(measurements, null), outcomeLines, goalLines,
            evaluation?.Id, evaluation?.ServiceDate, Since(evaluation?.ServiceDate),
            previousProgress?.Id, previousProgress?.ServiceDate, Since(previousProgress?.ServiceDate),
            string.IsNullOrWhiteSpace(homeProgram) ? null : homeProgram,
            episode.Count);
    }

    /// <summary>For each measurement: the reading at the cutoff (the last one
    /// on or before it; with no cutoff, the first) and the latest. A key with
    /// nothing newer than the cutoff is left out.</summary>
    private static IReadOnlyList<string> ChangeLines(IReadOnlyList<Reading> readings, DateOnly? cutoff) =>
        readings.GroupBy(r => r.Key).Select(g =>
        {
            var ordered = g.OrderBy(r => r.Date).ThenBy(r => r.Created).ToList();
            var latest = ordered[^1];
            var start = cutoff is DateOnly c ? ordered.LastOrDefault(r => r.Date <= c) : ordered[0];
            if (start is null || (cutoff is not null && latest.Date <= cutoff)) return null;
            return ReferenceEquals(start, latest)
                ? $"{latest.Label}: {latest.Value} ({D(latest.Date)})"
                : $"{latest.Label}: {start.Value} ({D(start.Date)}) → {latest.Value} ({D(latest.Date)})";
        }).OfType<string>().Take(25).ToList();

    private static IReadOnlyList<string> OutcomeLines(IReadOnlyList<OutcomeScore> scores, DateOnly? cutoff) =>
        scores.GroupBy(s => s.Measure).Select(g =>
        {
            var ordered = g.OrderBy(s => s.MeasuredOn).ToList();
            var latest = ordered[^1];
            var start = cutoff is DateOnly c ? ordered.LastOrDefault(s => s.MeasuredOn <= c) : ordered[0];
            if (start is null || (cutoff is not null && latest.MeasuredOn <= cutoff)) return null;
            var d = OutcomeMeasureCatalog.Get(g.Key);
            var interpretation = latest.Interpretation ?? OutcomeMeasureCatalog.Interpret(g.Key, latest.Score);
            return ReferenceEquals(start, latest)
                ? $"{d.Abbreviation}: {OutcomeMeasureCatalog.Format(g.Key, latest.Score)} ({D(latest.MeasuredOn)}) — {interpretation}"
                : $"{d.Abbreviation}: {OutcomeMeasureCatalog.Format(g.Key, start.Score)} ({D(start.MeasuredOn)}) → " +
                  $"{OutcomeMeasureCatalog.Format(g.Key, latest.Score)} ({D(latest.MeasuredOn)}), " +
                  $"{OutcomeMeasureCatalog.DescribeChange(g.Key, start.Score, latest.Score)} — {interpretation}";
        }).OfType<string>().ToList();

    private static string GoalLine(FunctionalGoal g)
    {
        var pct = g.ProgressPercent is int p ? $" ({p}%)" : "";
        var status = g.Status switch
        {
            GoalStatus.Active => "in progress",
            GoalStatus.NotStarted => "not started",
            GoalStatus.Met => "met",
            GoalStatus.PartiallyMet => "partially met",
            GoalStatus.Discontinued => "discontinued",
            _ => "draft",
        };
        return $"{(g.Term == GoalTerm.LongTerm ? "LTG" : "STG")}: {g.FunctionalTask.TrimEnd('.')} — baseline {Num(g.BaselineValue)}, " +
               $"now {(g.CurrentValue is decimal c ? Num(c) : "not measured")}, target {Num(g.TargetValue)} {g.Unit} by {D(g.TargetDate)}{pct}; {status}.";
    }

    private static string MeasurementLabel(ObjectiveMeasurement m) =>
        string.Join(" ", new[] { m.Item, m.Movement, m.Mode, m.Side is BodySide s ? $"({s})" : null }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string Dose(NoteIntervention i) =>
        i.Sets is int s && i.Repetitions is int r ? $"{s}x{r}" : i.Sets is int s2 ? $"{s2} sets" : i.Repetitions is int r2 ? $"{r2} reps" : "";

    private static string Rating(PainScaleType scale, decimal v) => scale switch
    {
        PainScaleType.VisualAnalog => $"{Num(v)} mm",
        PainScaleType.Verbal when v >= 0 && v < Verbal.Length && v == Math.Floor(v) => Verbal[(int)v],
        _ => $"{Num(v)}/10",
    };

    private static string Num(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string D(DateOnly d) => d.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ pre-fill

    public async Task<PrefillResultDto> PrefillAsync(Guid noteId, PrefillNoteRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanEditNote(actor, note) || !note.IsEditable)
            throw new ForbiddenException(note.IsEditable ? "You are not permitted to edit this note." : "This note can no longer be edited.");
        if (!EpisodeRules.IsSummaryNote(note.NoteType))
            throw new InvalidOperationException("Only progress notes, re-evaluations, recertifications and discharge summaries are filled from charting.");
        if (note.TemplateVersionId is not Guid versionId) throw new InvalidOperationException("This note has no template.");

        var latest = await _db.ClinicalNoteVersions.Where(v => v.NoteId == note.Id).OrderByDescending(v => v.VersionNumber)
            .Select(v => new { v.VersionNumber, v.CreatedAt, v.SavedById }).FirstOrDefaultAsync(ct);
        if ((latest?.VersionNumber ?? 0) != request.BaseSaveVersion)
        {
            var who = latest is null ? null : (await NamesAsync([latest.SavedById], ct)).GetValueOrDefault(latest.SavedById);
            throw new EncounterConflictException(latest?.VersionNumber ?? 0, latest?.CreatedAt ?? note.UpdatedAt, who);
        }

        var summary = await BuildEpisodeSummaryAsync(note, ct);
        var fields = (await _db.ClinicalNoteTemplateFields.AsNoTracking().Where(f => f.VersionId == versionId).ToListAsync(ct))
            .ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase);
        var hasValue = (await CurrentFieldValuesAsync(note.Id, ct)).Select(v => v.FieldKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var values = new List<TemplateFieldValueDto>();
        var filled = new List<string>();
        foreach (var proposal in Proposals(note, summary))
        {
            if (!fields.TryGetValue(proposal.Key, out var field)) continue;
            if (field.NoteColumn is string column)
            {
                if (!string.IsNullOrWhiteSpace(ColumnText(note, column)) || proposal.Text is null) continue;
                SetColumnText(note, column, proposal.Text);
            }
            else
            {
                if (hasValue.Contains(field.Key)) continue;
                values.Add(proposal);
            }
            filled.Add(field.Label);
        }
        if (values.Count > 0) await ApplyFieldValuesAsync(note, versionId, values, ct);

        // Goal status: add this episode's goals to the note (no value yet --
        // the therapist records it), when the template has a goals section.
        var goalsAdded = 0;
        if (await _db.ClinicalNoteTemplateSections.AnyAsync(s => s.VersionId == versionId && s.Component == "goals", ct))
        {
            var present = await _db.NoteGoalProgress.Where(p => p.NoteId == note.Id).Select(p => p.GoalId).ToListAsync(ct);
            var order = present.Count;
            var goals = await _db.FunctionalGoals.AsNoTracking()
                .Where(g => g.PatientId == note.PatientId && g.Status != GoalStatus.Draft && !present.Contains(g.Id) &&
                    ((g.Status == GoalStatus.NotStarted || g.Status == GoalStatus.Active) || (note.NoteType == NoteType.Discharge && summary.PlanOfCareId != null && g.PlanOfCareId == summary.PlanOfCareId)))
                .OrderBy(g => g.Term).ThenBy(g => g.TargetDate).ToListAsync(ct);
            foreach (var g in goals)
            {
                _db.NoteGoalProgress.Add(new NoteGoalProgress
                {
                    NoteId = note.Id,
                    GoalId = g.Id,
                    Order = order++,
                    GoalVersion = g.Version,
                    Term = g.Term,
                    FunctionalTask = g.FunctionalTask,
                    FunctionalLimitation = g.FunctionalLimitation,
                    BaselineValue = g.BaselineValue,
                    TargetValue = g.TargetValue,
                    Unit = g.Unit,
                    MeasurementMethod = g.MeasurementMethod,
                    TargetDate = g.TargetDate,
                    PreviousValue = g.CurrentValue,
                    CurrentValue = null,
                    Status = g.Status,
                    IncludeInNarrative = true,
                });
                goalsAdded++;
            }
        }

        var saveVersion = latest?.VersionNumber ?? 0;
        if (filled.Count > 0 || goalsAdded > 0)
        {
            note.PrefilledAt = DateTimeOffset.UtcNow;
            note.PrefilledById = actor.UserId;
            note.PrefillReviewedAt = null;
            note.PrefillReviewedById = null;
            note.UpdatedAt = DateTimeOffset.UtcNow;
            saveVersion = await SaveWithVersionSnapshotAsync(note, actor.UserId, isSignedVersion: false, ct);
            var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
            // Counts only: the generated text stays in the note, not the audit log.
            await _audit.RecordAuditEventAsync(actor.UserId, "note.prefilled", nameof(ClinicalNote), note.Id, organization.Id,
                patientId: note.PatientId, metadata: new { fields = filled.Count, goals = goalsAdded, signedNotes = summary.SignedNotesUsed }, ct: ct);
        }
        return new PrefillResultDto(saveVersion, filled, goalsAdded, summary);
    }

    /// <summary>Field values proposed for each kind of note (only empty fields are filled).</summary>
    private static IEnumerable<TemplateFieldValueDto> Proposals(ClinicalNote note, EpisodeSummaryDto s)
    {
        static string? Lines(IEnumerable<string?> lines)
        {
            var text = string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
            return text.Length == 0 ? null : text;
        }
        static TemplateFieldValueDto? Text(string key, string? text) => text is null ? null : new(key, Text: text);
        static TemplateFieldValueDto? Number(string key, int? n) => n is null ? null : new(key, Number: n);
        static TemplateFieldValueDto? Date(string key, DateOnly? d) => d is null ? null : new(key, Date: d);

        var objective = Lines(s.MeasurementChanges);
        var outcomesAndGoals = Lines([
            .. s.OutcomeChanges,
            s.GoalLines.Count > 0 ? "Goals:" : null,
            .. s.GoalLines]);
        var list = new List<TemplateFieldValueDto?>();
        switch (note.NoteType)
        {
            case NoteType.Progress:
                list.AddRange([
                    Date("periodStart", s.PeriodStart), Date("periodEnd", s.PeriodEnd), Number("visitsCompleted", s.VisitsInPeriod),
                    Text("subjectiveChanges", s.PainSummary), Text("objectiveChanges", objective),
                    Text("functionalImprovement", outcomesAndGoals),
                    Number("frequencyPerWeek", s.FrequencyPerWeek), Number("durationWeeks", s.DurationWeeks)]);
                break;
            case NoteType.ReEvaluation or NoteType.Recertification:
                var start = note.NoteType == NoteType.Recertification && s.PlanEnd is DateOnly end && end >= note.ServiceDate
                    ? end.AddDays(1) : note.ServiceDate;
                list.AddRange([
                    Text("updatedSubjective", s.PainSummary), Text("updatedObjective", objective),
                    Text("comparisonWithEvaluation", s.EvaluationDate is DateOnly ed && s.SinceEvaluation.Count > 0
                        ? Lines([$"Since the evaluation of {D(ed)}:", .. s.SinceEvaluation]) : null),
                    Text("comparisonWithProgress", s.PreviousProgressDate is DateOnly pd && s.SinceProgress.Count > 0
                        ? Lines([$"Since the progress note of {D(pd)}:", .. s.SinceProgress]) : null),
                    Number("frequencyPerWeek", s.FrequencyPerWeek), Number("durationWeeks", s.DurationWeeks),
                    Date("certificationStart", start),
                    Date("certificationEnd", s.DurationWeeks is int w ? start.AddDays(7 * w) : null)]);
                break;
            case NoteType.Discharge:
                list.AddRange([
                    Number("visitsCompleted", s.VisitsInEpisode), Text("attendance", s.Attendance.Text),
                    Text("finalSubjective", s.PainSummary), Text("finalObjective", objective),
                    Text("functionalOutcome", outcomesAndGoals), Text("homeProgram", s.HomeProgram)]);
                break;
        }
        return list.OfType<TemplateFieldValueDto>();
    }

    private static string? ColumnText(ClinicalNote note, string column) => column switch
    {
        "subjective" => note.Subjective,
        "objective" => note.Objective,
        "interventions" => note.Interventions,
        "assessment" => note.Assessment,
        "plan" => note.Plan,
        _ => null,
    };

    private static void SetColumnText(ClinicalNote note, string column, string text)
    {
        switch (column)
        {
            case "subjective": note.Subjective = text; break;
            case "objective": note.Objective = text; break;
            case "interventions": note.Interventions = text; break;
            case "assessment": note.Assessment = text; break;
            case "plan": note.Plan = text; break;
        }
    }

    /// <summary>The therapist confirms they reviewed the pre-filled content.</summary>
    public async Task<ClinicalNote> ReviewPrefillAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanEditNote(actor, note) || !note.IsEditable)
            throw new ForbiddenException(note.IsEditable ? "You are not permitted to edit this note." : "This note can no longer be edited.");
        if (note.PrefilledAt is null) throw new InvalidOperationException("Nothing on this note was pre-filled.");
        note.PrefillReviewedAt = DateTimeOffset.UtcNow;
        note.PrefillReviewedById = actor.UserId;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, "note.prefill_reviewed", nameof(ClinicalNote), note.Id, organization.Id,
            patientId: note.PatientId, ct: ct);
        return note;
    }
}
