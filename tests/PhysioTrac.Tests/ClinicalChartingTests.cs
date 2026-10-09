using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Clinical Charting on the existing ClinicalNote: structured
/// findings, editable interventions with patient response, and the 8-minute
/// rule summary.</summary>
public class ClinicalChartingTests
{
    private static (PhysioTracDbContext Db, ClinicalNoteService Notes, TestCurrentUser Therapist, Patient Pat) Setup()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Org", Slug = "org" }; // EightMinuteRuleVariant defaults to Medicare
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Quinn", LastName = "Alvarez", DateOfBirth = new DateOnly(2001, 7, 30) };
        db.Organizations.Add(org);
        db.Patients.Add(pat);
        db.SaveChanges();
        var audit = new AuditService(db);
        var notes = new ClinicalNoteService(db, new TenantAccessService(db, audit), audit, new AcceptAnySignature());
        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist };
        return (db, notes, therapist, pat);
    }

    private static Task<ClinicalNote> DraftAsync(ClinicalNoteService notes, TestCurrentUser who, Patient pat, string? objectiveJson = null) =>
        notes.CreateDraftAsync(new CreateNoteRequest(
            pat.Id, NoteType.Daily, new DateOnly(2026, 10, 6), null, "s", "Objective", null, "a", "Plan",
            null, null, null, null, null, ObjectiveMeasurementsJson: objectiveJson), who);

    private static UpdateNoteRequest Update(string? subjectiveJson = null, string? objectiveJson = null) =>
        new(null, null, null, null, null, null, null, null, null, null, subjectiveJson, objectiveJson);

    private static CreateInterventionRequest Item(string what, int minutes, bool timed = true, string? response = null) =>
        new(what, "Lumbar", InterventionCategory.TherapeuticExercise, minutes, null, timed, 0, response);

    [Fact]
    public async Task StructuredFindings_AreSaved_AndANullUpdateLeavesThemAlone()
    {
        var (_, notes, therapist, pat) = Setup();
        var rom = """{"rom":[{"joint":"Knee","motion":"Flexion","side":"R","arom":105}]}""";
        var note = await DraftAsync(notes, therapist, pat, rom);
        Assert.Equal(105, JsonDocument.Parse(note.ObjectiveMeasurementsJson).RootElement.GetProperty("rom")[0].GetProperty("arom").GetInt32());

        await notes.UpdateDraftAsync(note.Id, Update(subjectiveJson: """{"pain":{"now":4}}"""), therapist);
        var saved = await notes.GetAsync(note.Id, therapist);

        Assert.Contains("\"now\":4", saved.SubjectiveDetailsJson);
        Assert.Contains("Flexion", saved.ObjectiveMeasurementsJson); // untouched by the null
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("\"text\"")]
    public async Task StructuredFindings_MustBeAJsonObject(string bad)
    {
        var (_, notes, therapist, pat) = Setup();
        var note = await DraftAsync(notes, therapist, pat);

        await Assert.ThrowsAsync<InvalidOperationException>(() => notes.UpdateDraftAsync(note.Id, Update(objectiveJson: bad), therapist));
    }

    [Fact]
    public async Task StructuredFindings_HaveASizeLimit()
    {
        var (_, notes, therapist, pat) = Setup();
        var note = await DraftAsync(notes, therapist, pat);
        var huge = "{\"x\":\"" + new string('a', ChartJson.MaxLength) + "\"}";

        await Assert.ThrowsAsync<InvalidOperationException>(() => notes.UpdateDraftAsync(note.Id, Update(objectiveJson: huge), therapist));
    }

    [Fact]
    public async Task Interventions_CanBeAddedWithPatientResponse_Edited_AndRemoved()
    {
        var (_, notes, therapist, pat) = Setup();
        var note = await DraftAsync(notes, therapist, pat);

        var added = await notes.AddInterventionAsync(note.Id, Item("Bridges 3x10", 10, response: "Mild fatigue"), therapist);
        Assert.Equal("Mild fatigue", added.PatientResponse);

        var edited = await notes.UpdateInterventionAsync(note.Id, added.Id, Item("Bridges 3x12", 12, response: "Tolerated well"), therapist);
        Assert.Equal(("Bridges 3x12", 12, "Tolerated well"), (edited.Description, edited.Minutes, edited.PatientResponse));

        await notes.DeleteInterventionAsync(note.Id, added.Id, therapist);
        Assert.Empty(await notes.ListInterventionsAsync(note.Id, therapist));
    }

    [Fact]
    public async Task Interventions_AreValidated()
    {
        var (_, notes, therapist, pat) = Setup();
        var note = await DraftAsync(notes, therapist, pat);

        await Assert.ThrowsAsync<InvalidOperationException>(() => notes.AddInterventionAsync(note.Id, Item("  ", 10), therapist));
        await Assert.ThrowsAsync<InvalidOperationException>(() => notes.AddInterventionAsync(note.Id, Item("Gait", 481), therapist));
    }

    [Fact]
    public async Task Interventions_CannotChangeAfterSigning()
    {
        var (_, notes, therapist, pat) = Setup();
        var note = await DraftAsync(notes, therapist, pat);
        var item = await notes.AddInterventionAsync(note.Id, Item("Gait training", 15), therapist);
        await notes.SignNoteAsync(note.Id, true, null, therapist, "pw");

        await Assert.ThrowsAsync<ForbiddenException>(() => notes.UpdateInterventionAsync(note.Id, item.Id, Item("Changed", 20), therapist));
        await Assert.ThrowsAsync<ForbiddenException>(() => notes.DeleteInterventionAsync(note.Id, item.Id, therapist));
    }

    [Fact]
    public async Task Summary_TotalsTimedMinutes_AndAppliesTheOrganizationsEightMinuteRule()
    {
        var (_, notes, therapist, pat) = Setup();
        var note = await DraftAsync(notes, therapist, pat);
        await notes.AddInterventionAsync(note.Id, Item("TherEx", 23), therapist);
        await notes.AddInterventionAsync(note.Id, Item("Manual therapy", 15), therapist);
        await notes.AddInterventionAsync(note.Id, Item("Hot pack", 10, timed: false), therapist);

        var summary = await notes.SummarizeInterventionsAsync(note.Id, therapist);

        Assert.Equal(38, summary.TimedMinutes); // the untimed 10 aren't counted
        Assert.Equal(1, summary.UntimedCount);
        Assert.Equal(3, summary.EstimatedTimedUnits); // Medicare: 38-52 minutes = 3 units
        Assert.Equal("Medicare", summary.RuleVariant);
    }
}
