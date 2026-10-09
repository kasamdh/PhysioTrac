using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Seed;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Scoring for every outcome measure in the catalog.</summary>
public class OutcomeMeasureScoringTests
{
    private static List<OutcomeItemResponseDto> All(OutcomeMeasure m, decimal value) =>
        OutcomeMeasureCatalog.Get(m).Items.Select(i => new OutcomeItemResponseDto(i.Key, value)).ToList();

    private static List<OutcomeItemResponseDto> Values(params decimal[] values) =>
        values.Select((v, i) => new OutcomeItemResponseDto($"i{i + 1}", v)).ToList();

    private static OutcomeScoreResult Score(OutcomeMeasure m, IReadOnlyList<OutcomeItemResponseDto> r) => OutcomeMeasureCatalog.Score(m, r);

    [Fact]
    public void EveryMeasure_HasADefinition()
    {
        foreach (var m in Enum.GetValues<OutcomeMeasure>()) Assert.True(OutcomeMeasureCatalog.IsKnown(m), m.ToString());
        Assert.Equal(10, OutcomeMeasureCatalog.All.Count);
        Assert.Equal(OutcomeMeasureCatalog.All.Count, OutcomeMeasureCatalog.All.Select(d => d.Code).Distinct().Count());
    }

    [Fact]
    public void Lefs_SumsTwentyItems_OutOf80()
    {
        var r = Score(OutcomeMeasure.Lefs, All(OutcomeMeasure.Lefs, 4));
        Assert.Equal((80m, 80m), (r.Score, r.MaximumScore!.Value));
        Assert.Equal("100% of maximum function (higher is better)", r.Interpretation);
        Assert.Equal(40m, Score(OutcomeMeasure.Lefs, All(OutcomeMeasure.Lefs, 2)).Score);
        var missing = Score(OutcomeMeasure.Lefs, All(OutcomeMeasure.Lefs, 3).Skip(1).ToList());
        Assert.Contains("answer all 20 items (19 answered)", missing.Errors.Single());
    }

    [Fact]
    public void Odi_IsAPercentOfTheSectionsAnswered()
    {
        var r = Score(OutcomeMeasure.Odi, All(OutcomeMeasure.Odi, 2));
        Assert.Equal(40m, r.Score);
        Assert.Equal("Moderate disability (21–40%)", r.Interpretation);
        // One section skipped: 9 x 3 = 27 of 45 = 60%.
        var nine = Score(OutcomeMeasure.Odi, All(OutcomeMeasure.Odi, 3).Take(9).ToList());
        Assert.Equal((60m, "Severe disability (41–60%)"), (nine.Score, nine.Interpretation));
        Assert.False(Score(OutcomeMeasure.Odi, All(OutcomeMeasure.Odi, 3).Take(8).ToList()).IsValid);
        Assert.Equal("Minimal disability (0–20%)", Score(OutcomeMeasure.Odi, All(OutcomeMeasure.Odi, 0)).Interpretation);
    }

    [Fact]
    public void Ndi_IsOutOf50_ScaledWhenASectionIsSkipped()
    {
        var r = Score(OutcomeMeasure.Ndi, All(OutcomeMeasure.Ndi, 1));
        Assert.Equal((10m, "Mild disability (5–14)"), (r.Score, r.Interpretation));
        // 9 sections x 2 = 18, scaled to 10 sections = 20.
        var nine = Score(OutcomeMeasure.Ndi, All(OutcomeMeasure.Ndi, 2).Take(9).ToList());
        Assert.Equal((20m, "Moderate disability (15–24)"), (nine.Score, nine.Interpretation));
        Assert.Equal("Complete disability (35–50)", Score(OutcomeMeasure.Ndi, All(OutcomeMeasure.Ndi, 5)).Interpretation);
    }

    [Fact]
    public void QuickDash_IsMeanMinusOneTimes25_WithTenOfElevenItems()
    {
        Assert.Equal(50m, Score(OutcomeMeasure.QuickDash, All(OutcomeMeasure.QuickDash, 3)).Score);
        Assert.Equal(0m, Score(OutcomeMeasure.QuickDash, All(OutcomeMeasure.QuickDash, 1).Take(10).ToList()).Score);
        // (31 / 11 - 1) x 25 = 45.45 -> 45.5.
        Assert.Equal(45.5m, Score(OutcomeMeasure.QuickDash, Values(1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1)).Score);
        Assert.False(Score(OutcomeMeasure.QuickDash, All(OutcomeMeasure.QuickDash, 2).Take(9).ToList()).IsValid);
        Assert.Contains(Score(OutcomeMeasure.QuickDash, Values(0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1)).Errors, e => e.Contains("enter 1–5"));
    }

    [Fact]
    public void Psfs_AveragesThePatientsNamedActivities()
    {
        var r = Score(OutcomeMeasure.Psfs, [
            new("activity1", 3, "Climbing stairs"), new("activity2", 5, "Gardening"), new("activity3", 7, "Walking the dog")]);
        Assert.Equal(5m, r.Score);
        Assert.Equal(10m, r.MaximumScore);
        var unnamed = Score(OutcomeMeasure.Psfs, [new("activity1", 4)]);
        Assert.Contains("name the activity", unnamed.Errors.Single());
        Assert.Contains("at least 1 of 5", Score(OutcomeMeasure.Psfs, []).Errors.Single());
    }

    [Fact]
    public void Berg_SumsFourteenTasks_WithFallRiskBands()
    {
        Assert.Equal((56m, "Low fall risk (41–56)"), Pair(Score(OutcomeMeasure.Berg, All(OutcomeMeasure.Berg, 4))));
        Assert.Equal((28m, "Medium fall risk (21–40)"), Pair(Score(OutcomeMeasure.Berg, All(OutcomeMeasure.Berg, 2))));
        Assert.Equal((14m, "High fall risk (0–20)"), Pair(Score(OutcomeMeasure.Berg, All(OutcomeMeasure.Berg, 1))));
    }

    [Fact]
    public void Tug_IsTheMeanOfTheTrials_LowerIsBetter()
    {
        var r = Score(OutcomeMeasure.Tug, [new("trial1", 12.4m), new("trial2", 13.0m), new("trial3", 14.2m)]);
        Assert.Equal((13.2m, "Slower than typical (10–13.4 s)"), Pair(r));
        Assert.Equal("Increased fall risk (13.5 s or more, community-dwelling older adults)",
            Score(OutcomeMeasure.Tug, [new("trial1", 15)]).Interpretation);
        Assert.False(Score(OutcomeMeasure.Tug, [new("trial1", 0)]).IsValid);
        Assert.False(OutcomeMeasureCatalog.Get(OutcomeMeasure.Tug).HigherIsBetter);
    }

    [Fact]
    public void FiveTimesSitToStand_IsTheMeanOfTheTrials()
    {
        Assert.Equal((11m, "Typical (under 12 s)"), Pair(Score(OutcomeMeasure.FiveTimesSitToStand, [new("trial1", 10.5m), new("trial2", 11.5m)])));
        Assert.Equal("Increased fall risk (15 s or more, older adults)",
            Score(OutcomeMeasure.FiveTimesSitToStand, [new("trial1", 16.25m)]).Interpretation);
    }

    [Fact]
    public void Abc_IsTheMeanConfidenceOfSixteenItems()
    {
        Assert.Equal((60m, "Moderate level of functioning (50–79%)"), Pair(Score(OutcomeMeasure.Abc, All(OutcomeMeasure.Abc, 60))));
        var mixed = All(OutcomeMeasure.Abc, 90).Select((r, i) => i < 4 ? r with { Value = 30 } : r).ToList();
        // (4 x 30 + 12 x 90) / 16 = 75.
        Assert.Equal(75m, Score(OutcomeMeasure.Abc, mixed).Score);
        Assert.Equal("High level of functioning (80% or more)", Score(OutcomeMeasure.Abc, All(OutcomeMeasure.Abc, 100)).Interpretation);
        Assert.False(Score(OutcomeMeasure.Abc, All(OutcomeMeasure.Abc, 101)).IsValid);
    }

    [Fact]
    public void Fga_SumsTenTasks_OutOf30()
    {
        Assert.Equal((20m, "Increased fall risk (22 or less, older adults)"), Pair(Score(OutcomeMeasure.Fga, All(OutcomeMeasure.Fga, 2))));
        Assert.Equal((30m, "Lower fall risk (23–30)"), Pair(Score(OutcomeMeasure.Fga, All(OutcomeMeasure.Fga, 3))));
    }

    [Fact]
    public void Responses_AreValidated()
    {
        var errors = Score(OutcomeMeasure.Berg, [.. All(OutcomeMeasure.Berg, 2), new("i99", 1), new("i1", 2)]).Errors;
        Assert.Contains(errors, e => e.Contains("\"i99\" is not an item"));
        Assert.Contains(errors, e => e.Contains("answered more than once"));
        Assert.Contains("use steps of 1", Score(OutcomeMeasure.Berg, Values(2.5m)).Errors.First());
        Assert.Empty(OutcomeMeasureCatalog.ValidateTotal(OutcomeMeasure.Lefs, 80));
        Assert.NotEmpty(OutcomeMeasureCatalog.ValidateTotal(OutcomeMeasure.Lefs, 81));
        Assert.NotEmpty(OutcomeMeasureCatalog.ValidateTotal(OutcomeMeasure.Tug, 0));
    }

    private static (decimal, string) Pair(OutcomeScoreResult r) => (r.Score, r.Interpretation);
}

/// <summary>Goals with history, goal progress documented in encounters, and
/// outcome scores attached to notes.</summary>
public class GoalsAndOutcomesTests
{
    private sealed record Ctx(PhysioTracDbContext Db, ClinicalNoteService Notes, FunctionalGoalService Goals, OutcomeScoreService Outcomes,
        Patient Pat, TestCurrentUser Therapist);

    private static async Task<Ctx> SetupAsync()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional Clinic", Slug = "fictional" };
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Taylor", LastName = "Sample", DateOfBirth = new DateOnly(1975, 6, 6) };
        db.Organizations.Add(org);
        db.Patients.Add(pat);
        db.SaveChanges();
        await SystemTemplateSeeder.SeedAsync(db);
        var audit = new AuditService(db);
        var tenant = new TenantAccessService(db, audit);
        return new Ctx(db, new ClinicalNoteService(db, tenant, audit, new AcceptAnySignature()), new FunctionalGoalService(db, tenant, audit),
            new OutcomeScoreService(db, tenant, audit), pat,
            new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist });
    }

    private static async Task<FunctionalGoal> StairsGoalAsync(Ctx c, bool approve = true)
    {
        var goal = await c.Goals.CreateAsync(new CreateGoalRequest(c.Pat.Id, "Unable to climb stairs without pain",
            "Climb 12 stairs reciprocally with one rail", GoalTerm.ShortTerm, 4, 12, "stairs", "Stair count",
            new DateOnly(2026, 11, 30), null, "Fictional goal"), c.Therapist);
        return approve ? await c.Goals.ApproveAsync(goal.Id, c.Therapist) : goal;
    }

    private static Task<ClinicalNote> DailyAsync(Ctx c, DateOnly date) =>
        c.Notes.CreateDraftAsync(new CreateNoteRequest(c.Pat.Id, NoteType.Daily, date, null,
            null, null, null, null, null, null, null, null, null, null), c.Therapist);

    private static async Task SaveProgressAsync(Ctx c, ClinicalNote note, params NoteGoalProgressDto[] rows)
    {
        var v = (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).SaveVersion;
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(v,
            [new("patientResponse", Text: "Tolerated"), new("continuedSkilledNeed", Text: "Yes")],
            Subjective: "s", Objective: "o", Assessment: "a", Plan: "p", GoalProgress: rows), c.Therapist);
    }

    [Fact]
    public async Task Goals_StartNotStarted_AndKeepEveryVersionInTheirHistory()
    {
        var c = await SetupAsync();
        var goal = await StairsGoalAsync(c);
        Assert.Equal(GoalStatus.NotStarted, goal.Status);

        await c.Goals.UpdateAsync(goal.Id, new UpdateGoalRequest("Unable to climb stairs without pain",
            "Climb 14 stairs reciprocally without a rail", GoalTerm.ShortTerm, 4, 14, "stairs", "Stair count",
            new DateOnly(2026, 12, 15), "Raised after review"), c.Therapist);
        var progressed = await c.Goals.UpdateProgressAsync(goal.Id, new UpdateGoalProgressRequest(8, null, "Halfway"), c.Therapist);
        Assert.Equal((GoalStatus.Active, 2, 40), (progressed.Status, progressed.Version, progressed.ProgressPercent!.Value));

        var history = await c.Goals.HistoryAsync(goal.Id, c.Therapist);
        Assert.Equal([GoalHistoryKind.Created, GoalHistoryKind.Approved, GoalHistoryKind.Edited, GoalHistoryKind.Progress],
            history.Select(h => h.Kind));
        // Version 1's wording and target survive the edit.
        Assert.Equal(("Climb 12 stairs reciprocally with one rail", 12m, 1), (history[1].Snapshot.FunctionalTask, history[1].Snapshot.TargetValue, history[1].GoalVersion));
        Assert.Equal(("Climb 14 stairs reciprocally without a rail", 2), (history[2].Snapshot.FunctionalTask, history[2].GoalVersion));
        Assert.Equal((8m, "Halfway", 40), (history[3].CurrentValue!.Value, history[3].Comment, history[3].ProgressPercent!.Value));
    }

    [Fact]
    public async Task GoalHistory_IsAppendOnly()
    {
        var c = await SetupAsync();
        await StairsGoalAsync(c);
        var row = c.Db.FunctionalGoalHistory.First();
        row.Comment = "rewritten";
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Db.SaveChangesAsync());
        Assert.Contains("Goal history cannot be changed", ex.Message);
    }

    [Fact]
    public async Task GoalRules_RefuseUnmeasurableGoals_AndAdviseOnWording()
    {
        var c = await SetupAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Goals.CreateAsync(new CreateGoalRequest(c.Pat.Id, "x", "Walk", GoalTerm.LongTerm,
            5, 5, "", "", new DateOnly(2026, 12, 1), null), c.Therapist));
        var draft = await StairsGoalAsync(c, approve: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Goals.UpdateProgressAsync(draft.Id, new UpdateGoalProgressRequest(5), c.Therapist));

        var today = new DateOnly(2026, 10, 8);
        Assert.Equal(4, GoalRules.WordingAdvice("Improve walking", null, today.AddDays(-1), today).Count);
        Assert.Empty(GoalRules.WordingAdvice("Walk 300 ft independently on level ground", "Distance", today.AddDays(30), today));
    }

    [Fact]
    public async Task EncounterGoalProgress_IsSnapshotted_AndReachesTheGoalOnlyWhenSigned()
    {
        var c = await SetupAsync();
        var goal = await StairsGoalAsync(c);
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        await SaveProgressAsync(c, note, new NoteGoalProgressDto(goal.Id, 8, GoalStatus.Active, "Using one rail"));

        var row = c.Db.NoteGoalProgress.Single(p => p.NoteId == note.Id);
        Assert.Equal(("Climb 12 stairs reciprocally with one rail", 4m, 12m, (decimal?)null, 50), (row.FunctionalTask, row.BaselineValue, row.TargetValue, row.PreviousValue, row.ProgressPercent!.Value));
        Assert.Null(c.Db.FunctionalGoals.Single().CurrentValue); // not applied while the note is a draft

        var encounter = await c.Notes.GetEncounterAsync(note.Id, c.Therapist);
        Assert.Equal((8m, GoalStatus.Active), (encounter.GoalProgress!.Single().CurrentValue!.Value, encounter.GoalProgress!.Single().Status));
        Assert.Contains("Using one rail", c.Db.ClinicalNoteVersions.Where(v => v.NoteId == note.Id).OrderBy(v => v.VersionNumber).Last().ContentJson);

        await c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw");
        var signed = c.Db.FunctionalGoals.Single();
        Assert.Equal((8m, GoalStatus.Active), (signed.CurrentValue!.Value, signed.Status));
        var entry = c.Db.FunctionalGoalHistory.Single(h => h.Kind == GoalHistoryKind.Progress);
        Assert.Equal((note.Id, "Using one rail"), (entry.NoteId!.Value, entry.Comment));

        // Editing the goal later leaves the signed note's copy as written.
        await c.Goals.UpdateAsync(goal.Id, new UpdateGoalRequest("x", "Climb 20 stairs", GoalTerm.LongTerm, 4, 20, "stairs", "Stair count",
            new DateOnly(2027, 1, 1), null), c.Therapist);
        var kept = (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).GoalProgress!.Single();
        Assert.Equal(("Climb 12 stairs reciprocally with one rail", 12m), (kept.FunctionalTask, kept.TargetValue));
        Assert.NotNull(c.Db.ClinicalNotes.Single(n => n.Id == note.Id).SignatureHash);
    }

    [Fact]
    public async Task NextVisit_SeesThePreviousValue_AndCanMarkTheGoalMet()
    {
        var c = await SetupAsync();
        var goal = await StairsGoalAsync(c);
        var first = await DailyAsync(c, new DateOnly(2026, 10, 1));
        await SaveProgressAsync(c, first, new NoteGoalProgressDto(goal.Id, 8, GoalStatus.Active, null));
        await c.Notes.SignNoteAsync(first.Id, true, null, c.Therapist, "pw");

        var second = await DailyAsync(c, new DateOnly(2026, 10, 8));
        await SaveProgressAsync(c, second, new NoteGoalProgressDto(goal.Id, 12, GoalStatus.Met, "Independent", IncludeInNarrative: false));
        var row = (await c.Notes.GetEncounterAsync(second.Id, c.Therapist)).GoalProgress!.Single();
        Assert.Equal((8m, 100, false), (row.PreviousValue!.Value, row.ProgressPercent!.Value, row.IncludeInNarrative));

        await c.Notes.SignNoteAsync(second.Id, true, null, c.Therapist, "pw");
        Assert.Equal(GoalStatus.Met, c.Db.FunctionalGoals.Single().Status);
        Assert.Equal(2, c.Db.FunctionalGoalHistory.Count(h => h.Kind == GoalHistoryKind.Progress));
    }

    [Fact]
    public async Task EncounterGoalProgress_RefusesDraftGoals_AndOtherPatientsGoals()
    {
        var c = await SetupAsync();
        var draft = await StairsGoalAsync(c, approve: false);
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        var ex = await Assert.ThrowsAsync<TemplateValidationException>(() =>
            SaveProgressAsync(c, note, new NoteGoalProgressDto(draft.Id, 5, GoalStatus.Active, null)));
        Assert.Contains("must be approved", ex.Errors.Single());
        ex = await Assert.ThrowsAsync<TemplateValidationException>(() =>
            SaveProgressAsync(c, note, new NoteGoalProgressDto(Guid.NewGuid(), 5, GoalStatus.Active, null)));
        Assert.Contains("not one of this patient's goals", ex.Errors.Single());
        var approved = await c.Goals.ApproveAsync(draft.Id, c.Therapist);
        ex = await Assert.ThrowsAsync<TemplateValidationException>(() =>
            SaveProgressAsync(c, note, new NoteGoalProgressDto(approved.Id, 5, GoalStatus.Draft, null)));
        Assert.Contains("choose Not started", ex.Errors.Single());
    }

    [Fact]
    public async Task OutcomeScores_AreScoredOnTheServer_AndLockedWithTheirSignedNote()
    {
        var c = await SetupAsync();
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        var responses = OutcomeMeasureCatalog.Get(OutcomeMeasure.Berg).Items.Select(i => new OutcomeItemResponseDto(i.Key, 3)).ToList();
        var score = await c.Outcomes.RecordAsync(new RecordOutcomeScoreRequest(c.Pat.Id, note.Id, OutcomeMeasure.Berg,
            new DateOnly(2026, 10, 7), Score: 1, MaximumScore: null, Notes: "No device", ItemResponses: responses), c.Therapist);
        Assert.Equal((42m, 56m, "Low fall risk (41–56)"), (score.Score, score.MaximumScore!.Value, score.Interpretation));

        var dto = (await c.Outcomes.ListDtosForPatientAsync(c.Pat.Id, c.Therapist)).Single();
        Assert.Equal((14, false), (dto.ItemResponses!.Count, dto.IsLocked));
        Assert.Equal(42m, (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).Outcomes!.Single().Score);

        await SaveProgressAsync(c, note);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw");
        Assert.True((await c.Outcomes.ListDtosForPatientAsync(c.Pat.Id, c.Therapist)).Single().IsLocked);
        Assert.Contains("\"Score\":42", c.Db.ClinicalNoteVersions.Where(v => v.NoteId == note.Id).OrderBy(v => v.VersionNumber).Last().ContentJson);

        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Outcomes.RecordAsync(new RecordOutcomeScoreRequest(c.Pat.Id, null,
            OutcomeMeasure.Berg, new DateOnly(2026, 10, 7), 50, null, null), c.Therapist));
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Outcomes.DeleteAsync(score.Id, c.Therapist));
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Outcomes.RecordAsync(new RecordOutcomeScoreRequest(c.Pat.Id, note.Id,
            OutcomeMeasure.Lefs, new DateOnly(2026, 10, 7), 50, null, null), c.Therapist));
    }

    [Fact]
    public async Task OutcomeScores_OnADraft_CanBeRemoved_AndTotalsStillWork()
    {
        var c = await SetupAsync();
        var total = await c.Outcomes.RecordAsync(new RecordOutcomeScoreRequest(c.Pat.Id, null, OutcomeMeasure.Lefs,
            new DateOnly(2026, 10, 1), 52, 80, null), c.Therapist);
        Assert.Equal(("65% of maximum function (higher is better)", "{}"), (total.Interpretation, total.ItemResponsesJson));
        var bad = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Outcomes.RecordAsync(new RecordOutcomeScoreRequest(c.Pat.Id, null,
            OutcomeMeasure.Odi, new DateOnly(2026, 10, 1), null, null, null,
            [new OutcomeItemResponseDto("i1", 9)]), c.Therapist));
        Assert.Contains("enter 0–5", bad.Message);

        await c.Outcomes.DeleteAsync(total.Id, c.Therapist);
        Assert.Empty(c.Db.OutcomeScores);
    }
}
