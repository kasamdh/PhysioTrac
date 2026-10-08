using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Ai;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Seed;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>AI-assisted drafting: suggestions only (the note never
/// changes), a de-identified context, editors of an open note only, audit
/// rows without content, and the signature marked AI-assisted when a
/// suggestion was inserted.</summary>
public class DocumentationAiTests
{
    private const string Narrative = "Fictional narrative mentioning the neighbor Robin.";

    /// <summary>Records what it was given; returns fixed text.</summary>
    private sealed class CapturingProvider : IDocumentationAiProvider
    {
        public AiDocumentationContext? Seen;
        public string Name => "Capture";
        public Task<string> DraftAsync(AiDraftSection section, AiDocumentationContext context, CancellationToken ct = default)
        {
            Seen = context;
            return Task.FromResult($"Draft {section}");
        }
    }

    private sealed record Ctx(PhysioTracDbContext Db, ClinicalNoteService Notes, TenantAccessService Access, AuditService Audit,
        Organization Org, Patient Pat, TestCurrentUser Pt);

    private static async Task<Ctx> SetupAsync()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional Clinic", Slug = "fictional", Timezone = "America/Chicago" };
        var pat = new Patient
        {
            OrganizationId = org.Id, FirstName = "Taylor", LastName = "Samplename", DateOfBirth = new DateOnly(1975, 6, 6),
            MedicalRecordNumber = "SM-AITEST0001", Phone = "555-0100",
        };
        db.Organizations.Add(org);
        db.Patients.Add(pat);
        await db.SaveChangesAsync();
        await SystemTemplateSeeder.SeedAsync(db);
        var audit = new AuditService(db);
        var access = new TenantAccessService(db, audit);
        return new Ctx(db, new ClinicalNoteService(db, access, audit, new AcceptAnySignature()), access, audit, org, pat,
            new TestCurrentUser { UserId = TestTherapists.Add(db, org.Id), OrganizationId = org.Id, Role = UserRole.Therapist });
    }

    private static DocumentationAiService Ai(Ctx c, IDocumentationAiProvider? provider) => new(c.Notes, c.Access, c.Audit, provider);

    /// <summary>A visit note with pain, a measurement, a flowsheet row and narrative.</summary>
    private static async Task<Guid> ChartedVisitAsync(Ctx c)
    {
        var at = new DateTimeOffset(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);
        var visit = new Appointment { PatientId = c.Pat.Id, TherapistId = c.Pt.UserId, StartsAt = at, EndsAt = at.AddMinutes(45) };
        c.Db.Appointments.Add(visit);
        await c.Db.SaveChangesAsync();
        var noteId = (await c.Notes.OpenAppointmentEncounterAsync(visit.Id, c.Pt)).NoteId;
        var v = (await c.Notes.GetEncounterAsync(noteId, c.Pt)).SaveVersion;
        await c.Notes.SaveEncounterAsync(noteId, new SaveEncounterRequest(v,
            [new("patientResponse", Text: "Tolerated"), new("continuedSkilledNeed", Text: "Yes")],
            Subjective: Narrative, Objective: "o", Assessment: "Clinician assessment.", Plan: "Clinician plan.",
            Pain: new PainAssessmentDto(Current: 5, Worst: 8, BeforeTreatment: 6, AfterTreatment: 3, Location: "Right knee"),
            Measurements: [new ObjectiveMeasurementDto(MeasurementCategory.RangeOfMotion, "Knee", "Flexion", BodySide.Right, "AROM", 110, Unit: "deg")],
            Flowsheet: [new FlowsheetEntryDto("Therapeutic exercise", InterventionCategory.TherapeuticExercise, "97110", Minutes: 15)]), c.Pt);
        return noteId;
    }

    [Fact]
    public async Task MockDraft_IsASuggestionOnly_AndNeverChangesTheNote()
    {
        var c = await SetupAsync();
        var noteId = await ChartedVisitAsync(c);
        var before = await c.Notes.GetEncounterAsync(noteId, c.Pt);

        var draft = await Ai(c, new MockDocumentationAiProvider()).DraftAsync(noteId, AiDraftSection.Assessment, c.Pt);

        Assert.Equal(("Mock", AiDraftSection.Assessment), (draft.Provider, draft.Section));
        Assert.Contains("decreased from 6/10 before treatment to 3/10 after treatment", draft.Text);
        Assert.Contains("Knee Flexion AROM (Right) 110°", draft.Text);
        Assert.Contains("Therapeutic exercise (15 min)", draft.Text);
        Assert.Contains("[Clinician:", draft.Text); // judgment is left to the clinician
        Assert.Contains("never signs", draft.Notice);

        var after = await c.Notes.GetEncounterAsync(noteId, c.Pt);
        Assert.Equal((before.SaveVersion, NoteStatus.Draft, "Clinician assessment."), (after.SaveVersion, after.Note.Status, after.Note.Assessment));

        var row = c.Db.AuditEvents.Single(e => e.Action == AiRules.DraftedAction);
        Assert.Equal((noteId, c.Pat.Id), (row.ObjectId!.Value, row.PatientId!.Value));
        Assert.Contains("\"section\":\"Assessment\"", row.MetadataJson);
        Assert.DoesNotContain("decreased", row.MetadataJson);
    }

    [Fact]
    public async Task TheMockIsDeterministic_AndDraftsAPlan()
    {
        var c = await SetupAsync();
        var noteId = await ChartedVisitAsync(c);
        var ai = Ai(c, new MockDocumentationAiProvider());
        var first = await ai.DraftAsync(noteId, AiDraftSection.Plan, c.Pt);
        var second = await ai.DraftAsync(noteId, AiDraftSection.Plan, c.Pt);
        Assert.Equal(first.Text, second.Text);
        Assert.Contains("continue and progress Therapeutic exercise", first.Text);
    }

    [Fact]
    public async Task TheProviderGetsNoIdentifiersOrNarrative()
    {
        var c = await SetupAsync();
        var noteId = await ChartedVisitAsync(c);
        var provider = new CapturingProvider();
        await Ai(c, provider).DraftAsync(noteId, AiDraftSection.Assessment, c.Pt);

        var json = JsonSerializer.Serialize(provider.Seen);
        foreach (var identifier in new[] { "Taylor", "Samplename", "SM-AITEST0001", "1975", "555-0100", "Robin", "Fictional Clinic", "2026" })
            Assert.DoesNotContain(identifier, json);
        Assert.Equal((5m, "10"), (provider.Seen!.Pain!.Current!.Value, provider.Seen.Pain.ScaleMaximum));
    }

    [Fact]
    public async Task OnlyEditorsOfAnOpenNote_CanUseAI()
    {
        var c = await SetupAsync();
        var noteId = await ChartedVisitAsync(c);
        var ai = Ai(c, new MockDocumentationAiProvider());

        var biller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = c.Org.Id, Role = UserRole.Biller };
        await Assert.ThrowsAsync<ForbiddenException>(() => ai.DraftAsync(noteId, AiDraftSection.Plan, biller));

        var other = new Organization { Name = "Other", Slug = "other" };
        c.Db.Organizations.Add(other);
        await c.Db.SaveChangesAsync();
        var outsider = new TestCurrentUser { UserId = TestTherapists.Add(c.Db, other.Id), OrganizationId = other.Id, Role = UserRole.Therapist };
        await Assert.ThrowsAsync<NotFoundException>(() => ai.DraftAsync(noteId, AiDraftSection.Plan, outsider));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Ai(c, null).DraftAsync(noteId, AiDraftSection.Plan, c.Pt));
        Assert.Equal(new AiStatusDto(false, "None"), Ai(c, null).GetStatus());

        await c.Notes.SignNoteAsync(noteId, true, null, c.Pt, "pw");
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => ai.DraftAsync(noteId, AiDraftSection.Plan, c.Pt));
        Assert.Contains("only available while a note is being written", ex.Message);
        Assert.DoesNotContain(c.Db.AuditEvents, e => e.Action == AiRules.DraftedAction);
    }

    [Fact]
    public async Task InsertingADraft_MarksTheSignatureAiAssisted()
    {
        var c = await SetupAsync();
        var ai = Ai(c, new MockDocumentationAiProvider());
        var assisted = await ChartedVisitAsync(c);
        await ai.RecordInsertedAsync(assisted, AiDraftSection.Assessment, c.Pt);
        await c.Notes.SignNoteAsync(assisted, true, null, c.Pt, "pw");

        var plain = await ChartedVisitAsync(c);
        await c.Notes.SignNoteAsync(plain, true, null, c.Pt, "pw");

        bool AiAssisted(Guid id) => JsonDocument.Parse(c.Db.AuditEvents.Single(e => e.ObjectId == id && e.Action == "note.signed").MetadataJson)
            .RootElement.GetProperty("aiAssisted").GetBoolean();
        Assert.True(AiAssisted(assisted));
        Assert.False(AiAssisted(plain));
        Assert.Equal(NoteStatus.Signed, (await c.Notes.GetAsync(assisted, c.Pt)).Status); // signed by the clinician, not AI
    }
}
