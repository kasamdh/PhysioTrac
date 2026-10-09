using System.Text.Json;
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

/// <summary>The documentation audit trail (every lifecycle event in one
/// shape, views, updates, prints and exports, no clinical content) and the
/// permissions on printing and exporting.</summary>
public class DocumentationAuditTests
{
    private const string Narrative = "Fictional narrative that must never reach the audit log.";

    private sealed record Ctx(PhysioTracDbContext Db, ClinicalNoteService Notes, Organization Org, Patient Pat,
        TestCurrentUser Pta, TestCurrentUser Pt, TestCurrentUser Admin);

    private static async Task<Ctx> SetupAsync()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional Clinic", Slug = "fictional", Timezone = "America/Chicago" };
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Taylor", LastName = "Sample", DateOfBirth = new DateOnly(1975, 6, 6) };
        db.Organizations.Add(org);
        db.Patients.Add(pat);
        await db.SaveChangesAsync();
        await SystemTemplateSeeder.SeedAsync(db);
        var audit = new AuditService(db);
        return new Ctx(db, new ClinicalNoteService(db, new TenantAccessService(db, audit), audit, new AcceptAnySignature()), org, pat,
            new TestCurrentUser { UserId = TestTherapists.Add(db, org.Id, UserRole.Assistant), OrganizationId = org.Id, Role = UserRole.Assistant },
            new TestCurrentUser { UserId = TestTherapists.Add(db, org.Id), OrganizationId = org.Id, Role = UserRole.Therapist },
            new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin });
    }

    private static async Task SaveAsync(Ctx c, Guid noteId, TestCurrentUser who, string objective)
    {
        var v = (await c.Notes.GetEncounterAsync(noteId, who)).SaveVersion;
        await c.Notes.SaveEncounterAsync(noteId, new SaveEncounterRequest(v,
            [new("patientResponse", Text: "Tolerated"), new("continuedSkilledNeed", Text: "Yes")],
            Subjective: Narrative, Objective: objective, Assessment: "a", Plan: "p"), who);
    }

    private Dictionary<string, JsonElement> Meta(AuditEvent e) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(e.MetadataJson)!;

    private List<AuditEvent> Events(Ctx c, Guid noteId) =>
        c.Db.AuditEvents.Where(e => e.ObjectId == noteId).OrderBy(e => e.CreatedAt).ToList();

    [Fact]
    public async Task EveryLifecycleEvent_IsAudited_InOneShape()
    {
        var c = await SetupAsync();
        var at = new DateTimeOffset(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);
        var visit = new Appointment { PatientId = c.Pat.Id, TherapistId = c.Pta.UserId, StartsAt = at, EndsAt = at.AddMinutes(45) };
        c.Db.Appointments.Add(visit);
        await c.Db.SaveChangesAsync();
        var noteId = (await c.Notes.OpenAppointmentEncounterAsync(visit.Id, c.Pta)).NoteId;

        await SaveAsync(c, noteId, c.Pta, "o1");
        await SaveAsync(c, noteId, c.Pta, "o2"); // a second autosave isn't audited again
        await c.Notes.RecordNoteViewAsync(noteId, c.Pta);
        await c.Notes.RecordNoteViewAsync(noteId, c.Pta);
        await c.Notes.SignNoteAsync(noteId, true, null, c.Pta, "pw");
        await c.Notes.StartReviewAsync(noteId, c.Pt);
        await c.Notes.RecordNoteViewAsync(noteId, c.Pt);
        await c.Notes.ReturnForCorrectionAsync(noteId, "Add the gait response.", c.Pt);
        await SaveAsync(c, noteId, c.Pta, "o3");
        await c.Notes.SignNoteAsync(noteId, true, null, c.Pta, "pw");
        await c.Notes.CosignNoteAsync(noteId, c.Pt, "pw");
        await c.Notes.RecordNoteOutputAsync(noteId, "print", c.Pt);
        await c.Notes.RecordNoteOutputAsync(noteId, "export", c.Pt);
        var amendment = await c.Notes.CreateAmendmentAsync(noteId, new CreateAmendmentRequest("Wrong side documented."), c.Admin);
        await c.Notes.VoidNoteAsync(amendment.Id, new VoidNoteRequest("Started in error."), c.Admin);

        var events = Events(c, noteId);
        Assert.Equal(["note.created", "note.updated", "note.viewed", "note.submitted_for_cosign", "note.review_started", "note.viewed",
                "note.returned_for_correction", "note.submitted_for_cosign", "note.cosigned", "note.printed", "note.exported"],
            events.Select(e => e.Action));

        var submitted = events.First(e => e.Action == "note.submitted_for_cosign");
        var m = Meta(submitted);
        Assert.Equal((noteId.ToString(), noteId.ToString(), visit.Id.ToString(), "Draft", "ReviewRequired", "America/Chicago"),
            (m["noteId"].GetString(), m["encounterId"].GetString(), m["appointmentId"].GetString(), m["previousStatus"].GetString(),
             m["newStatus"].GetString(), m["displayTimeZone"].GetString()));
        Assert.Equal((c.Pta.UserId, c.Pat.Id), (submitted.ActorId!.Value, submitted.PatientId!.Value));
        Assert.Equal(TimeSpan.Zero, submitted.CreatedAt.Offset);

        Assert.Equal("Add the gait response.", Meta(events.Single(e => e.Action == "note.returned_for_correction"))["returnReason"].GetString());
        Assert.Equal(("ReviewRequired", "Signed"),
            (Meta(events.Single(e => e.Action == "note.cosigned"))["previousStatus"].GetString(), Meta(events.Single(e => e.Action == "note.cosigned"))["newStatus"].GetString()));

        var started = Events(c, amendment.Id).Single(e => e.Action == "note.amendment_started");
        Assert.Equal("Wrong side documented.", Meta(started)["amendmentReason"].GetString());
        var voided = Events(c, amendment.Id).Single(e => e.Action == "note.voided");
        Assert.Equal(("Started in error.", "Draft", "Voided"),
            (Meta(voided)["voidReason"].GetString(), Meta(voided)["previousStatus"].GetString(), Meta(voided)["newStatus"].GetString()));

        // Every event reads as a sentence on the Logs page, not a raw action code.
        foreach (var e in c.Db.AuditEvents.ToList())
            Assert.NotEqual(e.Action.Replace('_', ' ').Replace('.', ' '),
                PhysioTrac.Api.Controllers.AuditDescriber.Describe(e.Action, e.ObjectType, e.MetadataJson), StringComparer.OrdinalIgnoreCase);

        // No clinical content in any audit row.
        Assert.DoesNotContain(c.Db.AuditEvents.ToList(), e => e.MetadataJson.Contains("Fictional narrative") || e.MetadataJson.Contains("o3"));
    }

    [Fact]
    public async Task PrintingAndExporting_FollowWhoMayViewTheNote()
    {
        var c = await SetupAsync();
        var note = await c.Notes.CreateDraftAsync(new CreateNoteRequest(c.Pat.Id, NoteType.Daily, new DateOnly(2026, 10, 7), null,
            null, null, null, null, null, null, null, null, null, null), c.Pt);

        var biller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = c.Org.Id, Role = UserRole.Biller };
        await Assert.ThrowsAnyAsync<Exception>(() => c.Notes.RecordNoteOutputAsync(note.Id, "print", biller));
        var otherOrg = new Organization { Name = "Other", Slug = "other" };
        c.Db.Organizations.Add(otherOrg);
        await c.Db.SaveChangesAsync();
        var outsider = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = otherOrg.Id, Role = UserRole.Admin };
        await Assert.ThrowsAsync<NotFoundException>(() => c.Notes.RecordNoteOutputAsync(note.Id, "export", outsider));
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.RecordNoteOutputAsync(note.Id, "fax", c.Pt));
        Assert.DoesNotContain(c.Db.AuditEvents, e => e.Action is "note.printed" or "note.exported");

        await c.Notes.RecordNoteOutputAsync(note.Id, "export", c.Admin);
        Assert.Single(c.Db.AuditEvents, e => e.Action == "note.exported" && e.ActorId == c.Admin.UserId);
    }

    [Fact]
    public async Task PatientReports_AreAudited_ForClinicalStaff()
    {
        var c = await SetupAsync();
        await c.Notes.RecordPatientReportOutputAsync(c.Pat.Id, "outcomes", "print", c.Pt);
        var e = c.Db.AuditEvents.Single(a => a.Action == "patient_report.printed");
        Assert.Equal((c.Pat.Id, "outcomes"), (e.PatientId!.Value, Meta(e)["report"].GetString()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.RecordPatientReportOutputAsync(c.Pat.Id, "everything", "print", c.Pt));
        var scheduler = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = c.Org.Id, Role = UserRole.Scheduler };
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.RecordPatientReportOutputAsync(c.Pat.Id, "goals", "export", scheduler));
        foreach (var report in ClinicalNoteService.PatientReports)
            await c.Notes.RecordPatientReportOutputAsync(c.Pat.Id, report, "export", c.Pt);
        Assert.Equal(5, c.Db.AuditEvents.Count(a => a.Action == "patient_report.exported"));
    }

    [Fact]
    public async Task TheBodyChartReport_UsesSignedNotesOnly()
    {
        var c = await SetupAsync();
        async Task<ClinicalNote> NoteWithFinding(DateOnly date, bool sign)
        {
            var n = new ClinicalNote { PatientId = c.Pat.Id, TherapistId = c.Pt.UserId, NoteType = NoteType.Daily, ServiceDate = date };
            c.Db.ClinicalNotes.Add(n);
            c.Db.BodyChartFindings.Add(new BodyChartFinding { NoteId = n.Id, PatientId = c.Pat.Id, Region = "knee", View = BodyView.Front,
                Side = BodySide.Right, FindingType = BodyFindingType.Pain, X = 0.4m, Y = 0.7m });
            await c.Db.SaveChangesAsync();
            if (sign) { n.Status = NoteStatus.Signed; await c.Db.SaveChangesAsync(); }
            return n;
        }
        var older = await NoteWithFinding(new DateOnly(2026, 9, 1), true);
        var newer = await NoteWithFinding(new DateOnly(2026, 10, 1), true);
        await NoteWithFinding(new DateOnly(2026, 10, 7), false);

        var charts = await c.Notes.GetBodyChartHistoryAsync(c.Pat.Id, c.Pt);
        Assert.Equal([newer.Id, older.Id], charts.Select(h => h.NoteId));
        Assert.Equal("knee", charts[0].Findings.Single().Region);
    }
}
