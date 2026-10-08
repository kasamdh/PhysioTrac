using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Seed;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Structured pain assessment and body chart: saved with the
/// encounter, validated, shown from the last signed visit for comparison,
/// carried into amendments, and frozen once the note is signed.</summary>
public class PainAndBodyChartTests
{
    private sealed record Ctx(PhysioTracDbContext Db, ClinicalNoteService Notes, Patient Pat, TestCurrentUser Therapist);

    private static async Task<Ctx> SetupAsync()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional Clinic", Slug = "fictional" };
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Morgan", LastName = "Sample", DateOfBirth = new DateOnly(1985, 5, 5) };
        db.Organizations.Add(org);
        db.Patients.Add(pat);
        db.SaveChanges();
        await SystemTemplateSeeder.SeedAsync(db);
        var audit = new AuditService(db);
        return new Ctx(db, new ClinicalNoteService(db, new TenantAccessService(db, audit), audit, new AcceptAnySignature()), pat,
            new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist });
    }

    private static Task<ClinicalNote> DailyAsync(Ctx c, DateOnly date) =>
        c.Notes.CreateDraftAsync(new CreateNoteRequest(c.Pat.Id, NoteType.Daily, date, null,
            null, null, null, null, null, null, null, null, null, null), c.Therapist);

    private static readonly PainAssessmentDto Pain = new(
        PainScaleType.NumericRating, Current: 6, Best: 2, Worst: 8, BeforeTreatment: 6, AfterTreatment: 3,
        Location: "Right knee, medial", Qualities: ["Aching", "Sharp"], Frequency: PainFrequency.Intermittent,
        Irritability: PainIrritability.Moderate, AggravatingFactors: "Stairs", EasingFactors: "Rest", SleepImpact: "Wakes once a night");

    private static readonly BodyChartFindingDto KneePain =
        new(BodyView.Front, "knee", BodySide.Right, 0.38m, 0.71m, BodyFindingType.Pain, 6, Annotation: "Medial joint line");
    private static readonly BodyChartFindingDto ShinTingling =
        new(BodyView.Front, "lowerLeg", BodySide.Right, 0.37m, 0.82m, BodyFindingType.Tingling, 3, RadiatesTo: "Dorsum of foot");

    /// <summary>Fills the Daily SOAP note's required fields so it can be signed.</summary>
    private static async Task SignAsync(Ctx c, ClinicalNote note)
    {
        var v = (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).SaveVersion;
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(v,
            [new("patientResponse", Text: "Tolerated well"), new("continuedSkilledNeed", Text: "Yes")],
            Subjective: "Better", Objective: "Flexion 110", Assessment: "Improving", Plan: "Continue"), c.Therapist);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw");
    }

    [Fact]
    public async Task PainAndFindings_SaveWithTheEncounter_AsStructuredRows()
    {
        var c = await SetupAsync();
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        var v = (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).SaveVersion;

        var saved = await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(v, Pain: Pain, BodyChart: [KneePain, ShinTingling]), c.Therapist);

        Assert.Equal(v + 1, saved.SaveVersion);
        var row = c.Db.PainAssessments.Single(p => p.NoteId == note.Id);
        Assert.Equal((6m, 3m, "Aching|Sharp", c.Pat.Id), (row.Current!.Value, row.AfterTreatment!.Value, row.Qualities!, row.PatientId));
        var findings = c.Db.BodyChartFindings.Where(f => f.NoteId == note.Id).OrderBy(f => f.Order).ToList();
        Assert.Equal(new[] { BodyFindingType.Pain, BodyFindingType.Tingling }, findings.Select(f => f.FindingType).ToArray());
        Assert.All(findings, f => Assert.Equal(c.Pat.Id, f.PatientId));

        var encounter = await c.Notes.GetEncounterAsync(note.Id, c.Therapist);
        Assert.Equal(new[] { "Aching", "Sharp" }, encounter.Pain!.Qualities!.ToArray());
        Assert.Equal("Medial joint line", encounter.BodyChart![0].Annotation);
        Assert.Contains("BodyChart", c.Db.ClinicalNoteVersions.Where(x => x.NoteId == note.Id).OrderByDescending(x => x.VersionNumber).First().ContentJson);
    }

    [Fact]
    public async Task AnEmptyPainAssessmentOrChart_ClearsThem()
    {
        var c = await SetupAsync();
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        var v = (await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(1, Pain: Pain, BodyChart: [KneePain]), c.Therapist)).SaveVersion;

        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(v, Pain: new PainAssessmentDto(), BodyChart: []), c.Therapist);

        Assert.Empty(c.Db.PainAssessments);
        Assert.Empty(c.Db.BodyChartFindings);
    }

    [Theory]
    [InlineData("vas")]
    [InlineData("faces")]
    [InlineData("quality")]
    [InlineData("region")]
    [InlineData("point")]
    [InlineData("tooMany")]
    public async Task InvalidPainOrChartData_IsRejected(string problem)
    {
        var c = await SetupAsync();
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        var request = problem switch
        {
            "vas" => new SaveEncounterRequest(1, Pain: new PainAssessmentDto(PainScaleType.VisualAnalog, Current: 120)),
            "faces" => new SaveEncounterRequest(1, Pain: new PainAssessmentDto(PainScaleType.Faces, Current: 5)),
            "quality" => new SaveEncounterRequest(1, Pain: new PainAssessmentDto(Qualities: ["Spicy"])),
            "region" => new SaveEncounterRequest(1, BodyChart: [KneePain with { Region = "tail" }]),
            "point" => new SaveEncounterRequest(1, BodyChart: [KneePain with { X = 1.4m }]),
            _ => new SaveEncounterRequest(1, BodyChart: Enumerable.Repeat(KneePain, PainRules.MaxFindings + 1).ToList()),
        };
        await Assert.ThrowsAsync<TemplateValidationException>(() => c.Notes.SaveEncounterAsync(note.Id, request, c.Therapist));
        Assert.Empty(c.Db.PainAssessments);
        Assert.Empty(c.Db.BodyChartFindings);
    }

    [Fact]
    public async Task TheLastSignedVisitsChart_IsOfferedForComparison()
    {
        var c = await SetupAsync();
        var first = await DailyAsync(c, new DateOnly(2026, 10, 1));
        await c.Notes.SaveEncounterAsync(first.Id, new SaveEncounterRequest(1, Pain: Pain, BodyChart: [KneePain]), c.Therapist);
        await SignAsync(c, first);
        var draftLater = await DailyAsync(c, new DateOnly(2026, 10, 3)); // unsigned: not a comparison source
        await c.Notes.SaveEncounterAsync(draftLater.Id, new SaveEncounterRequest(1, BodyChart: [ShinTingling]), c.Therapist);

        var today = await DailyAsync(c, new DateOnly(2026, 10, 7));
        var previous = (await c.Notes.GetEncounterAsync(today.Id, c.Therapist)).Previous!;

        Assert.Equal((first.Id, new DateOnly(2026, 10, 1)), (previous.NoteId, previous.ServiceDate));
        Assert.Equal(6, previous.Pain!.Current);
        Assert.Equal("knee", Assert.Single(previous.BodyChart).Region);
        Assert.Null((await c.Notes.GetEncounterAsync(first.Id, c.Therapist)).Previous);
    }

    [Fact]
    public async Task ASignedNotesChart_IsPreservedExactly()
    {
        var c = await SetupAsync();
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(1, Pain: Pain, BodyChart: [KneePain]), c.Therapist);
        await SignAsync(c, note);
        var version = (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).SaveVersion;

        await Assert.ThrowsAnyAsync<Exception>(() =>
            c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(version, BodyChart: []), c.Therapist));
        var finding = c.Db.BodyChartFindings.Single();
        finding.Severity = 1;
        Assert.Throws<InvalidOperationException>(() => c.Db.SaveChanges());
        c.Db.ChangeTracker.Clear();
        c.Db.PainAssessments.Remove(c.Db.PainAssessments.Single());
        Assert.Throws<InvalidOperationException>(() => c.Db.SaveChanges());
    }

    [Fact]
    public async Task PainHistory_ListsSignedVisitsInOrder()
    {
        var c = await SetupAsync();
        foreach (var (date, now) in new[] { (new DateOnly(2026, 10, 5), 4m), (new DateOnly(2026, 10, 1), 7m) })
        {
            var n = await DailyAsync(c, date);
            await c.Notes.SaveEncounterAsync(n.Id, new SaveEncounterRequest(1, Pain: Pain with { Current = now }), c.Therapist);
            await SignAsync(c, n);
        }
        var draft = await DailyAsync(c, new DateOnly(2026, 10, 7));
        await c.Notes.SaveEncounterAsync(draft.Id, new SaveEncounterRequest(1, Pain: Pain with { Current = 1 }), c.Therapist);

        var history = await c.Notes.GetPainHistoryAsync(c.Pat.Id, c.Therapist);
        Assert.Equal(new decimal?[] { 7, 4 }, history.Select(h => h.Current).ToArray());
    }

    [Fact]
    public async Task AnAmendment_CarriesThePainAndChart()
    {
        var c = await SetupAsync();
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(1, Pain: Pain, BodyChart: [KneePain, ShinTingling]), c.Therapist);
        await SignAsync(c, note);

        var amendment = await c.Notes.CreateAmendmentAsync(note.Id, new CreateAmendmentRequest("Wrong side"), c.Therapist);
        var copy = await c.Notes.GetEncounterAsync(amendment.Id, c.Therapist);
        Assert.Equal(6, copy.Pain!.Current);
        Assert.Equal(2, copy.BodyChart!.Count);
        // The original's rows are untouched.
        Assert.Equal(2, c.Db.BodyChartFindings.Count(f => f.NoteId == note.Id));
    }
}
