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

/// <summary>Progress notes, re-evaluations, recertifications and discharge
/// summaries: the episode summary (signed charting only), pre-filling, the
/// review required before signing, and plan-of-care effects.</summary>
public class EpisodeWorkflowTests
{
    private sealed record Ctx(PhysioTracDbContext Db, ClinicalNoteService Notes, FunctionalGoalService Goals, Patient Pat,
        TestCurrentUser Therapist, PlanOfCare Plan, FunctionalGoal Goal);

    private static readonly DateOnly Eval = new(2026, 9, 1);

    private static async Task<Ctx> SetupAsync()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional Clinic", Slug = "fictional" };
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Taylor", LastName = "Sample", DateOfBirth = new DateOnly(1958, 6, 6) };
        db.Organizations.Add(org);
        db.Patients.Add(pat);
        db.SaveChanges();
        await SystemTemplateSeeder.SeedAsync(db);
        var audit = new AuditService(db);
        var tenant = new TenantAccessService(db, audit);
        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist };
        var notes = new ClinicalNoteService(db, tenant, audit, new AcceptAnySignature());
        var goals = new FunctionalGoalService(db, tenant, audit);

        // A signed evaluation with pain, a measurement and a LEFS score; its plan of care.
        var eval = await SignedAsync(db, pat, therapist, NoteType.Evaluation, Eval, pain: 6, flexion: 95);
        var plan = new PlanOfCare
        {
            PatientId = pat.Id,
            SourceNoteId = eval.Id,
            Status = PlanOfCareStatus.Active,
            StartDate = Eval,
            EndDate = new DateOnly(2026, 10, 27),
            FrequencyPerWeek = 2,
            DurationWeeks = 8,
            HomeProgram = "Daily quad sets and heel slides.",
        };
        db.PlansOfCare.Add(plan);
        db.OutcomeScores.Add(new OutcomeScore { PatientId = pat.Id, NoteId = eval.Id, RecordedById = therapist.UserId, Measure = OutcomeMeasure.Lefs, MeasuredOn = Eval, Score = 40, MaximumScore = 80 });
        await db.SaveChangesAsync();

        var goal = await goals.CreateAsync(new CreateGoalRequest(pat.Id, "Stairs painful", "Climb 12 stairs reciprocally with one rail",
            GoalTerm.ShortTerm, 4, 12, "stairs", "Stair count", new DateOnly(2026, 10, 30), null), therapist);
        await goals.ApproveAsync(goal.Id, therapist);
        await goals.UpdateProgressAsync(goal.Id, new UpdateGoalProgressRequest(8), therapist);
        goal.PlanOfCareId = plan.Id;
        await db.SaveChangesAsync();

        // Two more signed visits, a draft that must be ignored, and a LEFS re-test.
        var visit = await SignedAsync(db, pat, therapist, NoteType.Daily, new DateOnly(2026, 9, 15), pain: 4, flexion: 110);
        await SignedAsync(db, pat, therapist, NoteType.Daily, new DateOnly(2026, 9, 29), pain: 3, flexion: 118, hep: true);
        await DraftWithPainAsync(db, pat, therapist, new DateOnly(2026, 9, 30));
        db.OutcomeScores.Add(new OutcomeScore { PatientId = pat.Id, NoteId = visit.Id, RecordedById = therapist.UserId, Measure = OutcomeMeasure.Lefs, MeasuredOn = new DateOnly(2026, 9, 15), Score = 52, MaximumScore = 80 });
        foreach (var (day, status) in new[] { (2, AppointmentStatus.Completed), (15, AppointmentStatus.Completed), (22, AppointmentStatus.NoShow),
            (25, AppointmentStatus.Cancelled), (29, AppointmentStatus.Completed) })
        {
            var at = new DateTimeOffset(2026, 9, day, 14, 0, 0, TimeSpan.Zero);
            db.Appointments.Add(new Appointment { PatientId = pat.Id, TherapistId = therapist.UserId, StartsAt = at, EndsAt = at.AddHours(1), Status = status });
        }
        await db.SaveChangesAsync();
        return new Ctx(db, notes, goals, pat, therapist, plan, goal);
    }

    private static async Task<ClinicalNote> SignedAsync(PhysioTracDbContext db, Patient pat, TestCurrentUser t, NoteType type, DateOnly date,
        decimal pain, decimal flexion, bool hep = false)
    {
        var note = new ClinicalNote { PatientId = pat.Id, TherapistId = t.UserId, NoteType = type, ServiceDate = date, Subjective = "s" };
        db.ClinicalNotes.Add(note);
        db.PainAssessments.Add(new PainAssessment { NoteId = note.Id, PatientId = pat.Id, Current = pain, Worst = pain + 2 });
        db.ObjectiveMeasurements.Add(new ObjectiveMeasurement
        {
            NoteId = note.Id,
            PatientId = pat.Id,
            Category = MeasurementCategory.RangeOfMotion,
            Item = "Knee",
            Movement = "Flexion",
            Side = BodySide.Right,
            Mode = "AROM",
            NumericValue = flexion,
            Unit = "deg"
        });
        if (hep)
            db.NoteInterventions.Add(new NoteIntervention { NoteId = note.Id, Description = "Bridges", Category = InterventionCategory.HomeExerciseProgram, Sets = 3, Repetitions = 10 });
        await db.SaveChangesAsync();
        note.Status = NoteStatus.Signed;
        note.SignedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return note;
    }

    private static async Task DraftWithPainAsync(PhysioTracDbContext db, Patient pat, TestCurrentUser t, DateOnly date)
    {
        var note = new ClinicalNote { PatientId = pat.Id, TherapistId = t.UserId, NoteType = NoteType.Daily, ServiceDate = date };
        db.ClinicalNotes.Add(note);
        db.PainAssessments.Add(new PainAssessment { NoteId = note.Id, PatientId = pat.Id, Current = 0 });
        await db.SaveChangesAsync();
    }

    private static Task<ClinicalNote> DraftAsync(Ctx c, NoteType type, DateOnly date) =>
        c.Notes.CreateDraftAsync(new CreateNoteRequest(c.Pat.Id, type, date, null,
            null, null, null, null, null, null, null, null, null, null), c.Therapist);

    private static async Task<int> VersionAsync(Ctx c, ClinicalNote note) => (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).SaveVersion;

    [Fact]
    public async Task TheEpisodeSummary_UsesSignedChartingOnly()
    {
        var c = await SetupAsync();
        var progress = await DraftAsync(c, NoteType.Progress, new DateOnly(2026, 10, 1));
        var s = await c.Notes.GetEpisodeSummaryAsync(progress.Id, c.Therapist);

        Assert.Equal((Eval, Eval, new DateOnly(2026, 10, 1)), (s.EpisodeStart, s.PeriodStart, s.PeriodEnd));
        Assert.Equal("since the evaluation of 09/01/2026", s.PeriodBasis);
        Assert.Equal((3, 3, 3), (s.VisitsInPeriod, s.VisitsInEpisode, s.SignedNotesUsed));
        Assert.Equal("3 of 5 scheduled visits attended (1 no-show, 1 cancellation).", s.Attendance.Text);
        // The draft's pain of 0 on 09/30 is not used.
        Assert.Equal("Pain now: 6/10 (09/01/2026) → 3/10 (09/29/2026)", s.PainSummary);
        Assert.Equal("Knee Flexion AROM (Right): 95 deg (09/01/2026) → 118 deg (09/29/2026)", s.MeasurementChanges.Single());
        Assert.Equal("LEFS: 40/80 (09/01/2026) → 52/80 (09/15/2026), +12 points, meaningful improvement — 65% of maximum function (higher is better)",
            s.OutcomeChanges.Single());
        Assert.Equal("STG: Climb 12 stairs reciprocally with one rail — baseline 4, now 8, target 12 stairs by 10/30/2026 (50%); in progress.",
            s.GoalLines.Single());
        Assert.Equal("Daily quad sets and heel slides.\nHome exercises: Bridges 3x10.", s.HomeProgram);
    }

    [Fact]
    public async Task AProgressNote_IsPrefilled_AndCantBeSignedUntilReviewed()
    {
        var c = await SetupAsync();
        var note = await DraftAsync(c, NoteType.Progress, new DateOnly(2026, 10, 1));
        // Something the therapist already wrote is never overwritten.
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(await VersionAsync(c, note), Subjective: "Patient reports less pain on stairs."), c.Therapist);

        var result = await c.Notes.PrefillAsync(note.Id, new PrefillNoteRequest(await VersionAsync(c, note)), c.Therapist);
        Assert.Equal(["Period start", "Period end", "Visits completed", "Objective changes", "Functional improvement", "Frequency (per week)", "Duration (weeks)"],
            result.FilledFields);
        Assert.Equal(1, result.GoalsAdded);

        var encounter = await c.Notes.GetEncounterAsync(note.Id, c.Therapist);
        Assert.Equal(result.SaveVersion, encounter.SaveVersion);
        Assert.Equal("Patient reports less pain on stairs.", encounter.Note.Subjective);
        Assert.StartsWith("Knee Flexion AROM (Right): 95 deg", encounter.Note.Objective);
        Assert.Equal(3m, encounter.Values.Single(v => v.Key == "visitsCompleted").Number);
        Assert.Contains("Goals:\nSTG: Climb 12 stairs", encounter.Values.Single(v => v.Key == "functionalImprovement").Text);
        Assert.Equal((c.Goal.Id, (decimal?)null, GoalStatus.Active), (encounter.GoalProgress!.Single().GoalId, encounter.GoalProgress!.Single().CurrentValue, encounter.GoalProgress!.Single().Status));
        Assert.NotNull(encounter.Note.PrefilledAt);
        Assert.Contains(c.Db.AuditEvents, e => e.Action == "note.prefilled");

        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(encounter.SaveVersion,
            [new("remainingImpairments", Text: "Reduced eccentric control.")],
            Assessment: "Continued skilled PT is needed for stair training.", Plan: "Progress eccentric loading."), c.Therapist);
        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw"));
        Assert.Contains("prefill_unreviewed", blocked.Message);

        await c.Notes.ReviewPrefillAsync(note.Id, c.Therapist);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw");
        Assert.Equal(NoteStatus.Signed, c.Db.ClinicalNotes.Single(n => n.Id == note.Id).Status);
        Assert.Equal(PlanOfCareStatus.Active, c.Db.PlansOfCare.Single().Status); // a progress note doesn't change the plan
    }

    [Fact]
    public async Task Prefill_RefusesStaleEditorsAndOtherNoteTypes()
    {
        var c = await SetupAsync();
        var note = await DraftAsync(c, NoteType.Progress, new DateOnly(2026, 10, 1));
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(await VersionAsync(c, note), Subjective: "x"), c.Therapist);
        await Assert.ThrowsAsync<EncounterConflictException>(() => c.Notes.PrefillAsync(note.Id, new PrefillNoteRequest(0), c.Therapist));

        var daily = await DraftAsync(c, NoteType.Daily, new DateOnly(2026, 10, 1));
        var dailyVersion = await VersionAsync(c, daily);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.PrefillAsync(daily.Id, new PrefillNoteRequest(dailyVersion), c.Therapist));
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.ReviewPrefillAsync(daily.Id, c.Therapist));
    }

    [Fact]
    public async Task ASecondProgressNote_CoversTheVisitsSinceTheFirst_AndPlanChangesAreFlagged()
    {
        var c = await SetupAsync();
        var first = await SignedAsync(c.Db, c.Pat, c.Therapist, NoteType.Progress, new DateOnly(2026, 9, 16), pain: 4, flexion: 112);
        var note = await DraftAsync(c, NoteType.Progress, new DateOnly(2026, 10, 1));
        var s = await c.Notes.GetEpisodeSummaryAsync(note.Id, c.Therapist);
        Assert.Equal((new DateOnly(2026, 9, 17), "since the progress note of 09/16/2026", 1), (s.PeriodStart, s.PeriodBasis, s.VisitsInPeriod));
        Assert.Equal(first.Id, s.PreviousProgressNoteId);
        Assert.Equal(["Pain now: 4/10 (09/16/2026) → 3/10 (09/29/2026)", "Knee Flexion AROM (Right): 112 deg (09/16/2026) → 118 deg (09/29/2026)"],
            s.SinceProgress);

        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(await VersionAsync(c, note),
            [new("frequencyPerWeek", Number: 3), new("durationWeeks", Number: 8)]), c.Therapist);
        var finding = (await c.Notes.GetComplianceAsync(note.Id, c.Therapist)).Single(f => f.Code == "plan_change_needs_recert");
        Assert.False(finding.FinalizationBlocker);
        Assert.Contains("2x/week for 8 weeks", finding.Detail);
    }

    [Fact]
    public async Task ARecertification_ProposesTheNextPeriod_ComparesWithTheEvaluation_AndKeepsThePreviousPlan()
    {
        var c = await SetupAsync();
        var note = await DraftAsync(c, NoteType.Recertification, new DateOnly(2026, 10, 20));
        await c.Notes.PrefillAsync(note.Id, new PrefillNoteRequest(await VersionAsync(c, note)), c.Therapist);
        var values = (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).Values.ToDictionary(v => v.Key);
        Assert.Equal((new DateOnly(2026, 10, 28), new DateOnly(2026, 12, 23)), (values["certificationStart"].Date!.Value, values["certificationEnd"].Date!.Value));
        Assert.Equal((2m, 8m), (values["frequencyPerWeek"].Number!.Value, values["durationWeeks"].Number!.Value));
        Assert.Equal("Since the evaluation of 09/01/2026:\nPain now: 6/10 (09/01/2026) → 3/10 (09/29/2026)\n" +
            "Knee Flexion AROM (Right): 95 deg (09/01/2026) → 118 deg (09/29/2026)\n" +
            "LEFS: 40/80 (09/01/2026) → 52/80 (09/15/2026), +12 points, meaningful improvement — 65% of maximum function (higher is better)",
            values["comparisonWithEvaluation"].Text);
        Assert.False(values.ContainsKey("comparisonWithProgress")); // no progress note yet

        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest((await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).SaveVersion,
            [new("reason", Text: "End of certification period"), new("changeInCondition", Text: "Improving"),
                new("updatedPrognosis", Text: "Good")],
            Assessment: "Continued skilled PT needed.", Plan: "Continue plan."), c.Therapist);
        await c.Notes.ReviewPrefillAsync(note.Id, c.Therapist);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw");

        var plans = c.Db.PlansOfCare.OrderBy(p => p.StartDate).ToList();
        Assert.Equal((PlanOfCareStatus.Superseded, "Daily quad sets and heel slides."), (plans[0].Status, plans[0].HomeProgram));
        Assert.Equal((PlanOfCareStatus.Active, new DateOnly(2026, 10, 28), plans[0].Id), (plans[1].Status, plans[1].StartDate, plans[1].PreviousPlanOfCareId!.Value));
    }

    [Fact]
    public async Task ASignedDischarge_ClosesThePlanWithItsReason()
    {
        var c = await SetupAsync();
        var note = await DraftAsync(c, NoteType.Discharge, new DateOnly(2026, 10, 2));
        var result = await c.Notes.PrefillAsync(note.Id, new PrefillNoteRequest(await VersionAsync(c, note)), c.Therapist);
        Assert.Equal(["Visits completed", "Attendance", "Final subjective status", "Final objective measurements", "Functional outcome", "Home program"],
            result.FilledFields);
        var encounter = await c.Notes.GetEncounterAsync(note.Id, c.Therapist);
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(encounter.SaveVersion,
            [new("dischargeReason", Text: "Goals met")],
            Assessment: "Independent with home program.", Plan: "Return if symptoms recur.",
            GoalProgress: [new NoteGoalProgressDto(c.Goal.Id, 12, GoalStatus.Met, "Independent")]), c.Therapist);
        await c.Notes.ReviewPrefillAsync(note.Id, c.Therapist);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw");

        var plan = c.Db.PlansOfCare.Single();
        Assert.Equal((PlanOfCareStatus.Discharged, DischargeReason.GoalsMet, note.Id), (plan.Status, plan.DischargeReason!.Value, plan.DischargeNoteId!.Value));
        Assert.Equal(GoalStatus.Met, c.Db.FunctionalGoals.Single().Status);
        Assert.Equal(DischargeReason.Other, EpisodeRules.DischargeReasonFor("Other documented reason"));
    }

    [Fact]
    public void ManyNotes_CanShareAPlanOfCare()
    {
        // The in-memory provider doesn't enforce unique indexes, so check the
        // relational model: a unique index here broke every note after the
        // first one in a plan of care.
        using var db = new PhysioTracDbContext(new DbContextOptionsBuilder<PhysioTracDbContext>().UseSqlServer("Server=.;Database=model-only").Options);
        var note = db.Model.FindEntityType(typeof(ClinicalNote))!;
        var index = note.GetIndexes().Single(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(ClinicalNote.PlanOfCareId));
        Assert.False(index.IsUnique);
    }
}
