using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Documentation database foundation: encounter links, status
/// history, typed electronic signatures, cosign requests, and the
/// persistence rules that keep signed clinical records unchanged.</summary>
public class DocumentationFoundationTests
{
    private sealed record Ctx(PhysioTracDbContext Db, ClinicalNoteService Notes, Organization Org, Patient Pat, TestCurrentUser Therapist, TestCurrentUser Admin);

    private static Ctx Setup(bool ptaCosign = false)
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional Clinic", Slug = "fictional", PtaCosignRequired = ptaCosign, Timezone = "America/Chicago" };
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Test", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Organizations.Add(org);
        db.Patients.Add(pat);
        db.SaveChanges();
        var audit = new AuditService(db);
        var notes = new ClinicalNoteService(db, new TenantAccessService(db, audit), audit, new AcceptAnySignature());
        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist };
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        return new Ctx(db, notes, org, pat, therapist, admin);
    }

    private static Task<ClinicalNote> DraftAsync(Ctx c, TestCurrentUser who, Guid? appointmentId = null) =>
        c.Notes.CreateDraftAsync(new CreateNoteRequest(
            c.Pat.Id, NoteType.Daily, new DateOnly(2026, 10, 7), appointmentId, "s", "Objective", null, "a", "Plan",
            null, null, null, null, null), who);

    [Fact]
    public async Task NewNote_LinksTheAppointmentsProvider_AndTheActivePlanOfCare()
    {
        var c = Setup();
        var provider = new Provider { OrganizationId = c.Org.Id, FirstName = "Jordan", LastName = "Example", UserId = c.Therapist.UserId };
        var appt = new Appointment
        {
            PatientId = c.Pat.Id,
            TherapistId = c.Therapist.UserId,
            ProviderId = provider.Id,
            StartsAt = DateTimeOffset.UtcNow,
            EndsAt = DateTimeOffset.UtcNow.AddMinutes(30)
        };
        var eval = new ClinicalNote { PatientId = c.Pat.Id, TherapistId = c.Therapist.UserId, NoteType = NoteType.Evaluation, Status = NoteStatus.Draft };
        var poc = new PlanOfCare
        {
            PatientId = c.Pat.Id,
            SourceNoteId = eval.Id,
            Status = PlanOfCareStatus.Active,
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 1)
        };
        c.Db.AddRange(provider, appt, eval, poc);
        await c.Db.SaveChangesAsync();

        var note = await DraftAsync(c, c.Therapist, appt.Id);

        Assert.Equal((provider.Id, poc.Id), (note.TreatingProviderId, note.PlanOfCareId));
        var change = Assert.Single(c.Db.ClinicalNoteStatusChanges.Where(s => s.NoteId == note.Id));
        Assert.Equal((null, NoteStatus.Draft), (change.FromStatus, change.ToStatus));
    }

    [Fact]
    public async Task Signing_RecordsAnAuthorSignature_ForTheExactSignedVersion_AndTheStatusChange()
    {
        var c = Setup();
        var note = await DraftAsync(c, c.Therapist);
        await c.Notes.SignNoteAsync(note.Id, true, "10.0.0.1", c.Therapist, "pw");

        var signature = Assert.Single(c.Db.ElectronicSignatures.Where(s => s.NoteId == note.Id));
        var signedVersion = c.Db.ClinicalNoteVersions.Single(v => v.NoteId == note.Id && v.IsSignedVersion);
        Assert.Equal(SignatureMeaning.Author, signature.Meaning);
        Assert.Equal(("Therapist", "America/Chicago", signedVersion.VersionNumber), (signature.Role, signature.DisplayTimeZone, signature.NoteVersionNumber));
        Assert.Equal(TimeSpan.Zero, signature.SignedAt.Offset); // UTC
        Assert.Contains(c.Db.ClinicalNoteStatusChanges, s => s.NoteId == note.Id && s.FromStatus == NoteStatus.Draft && s.ToStatus == NoteStatus.Signed);

        var history = await c.Notes.GetNoteHistoryAsync(note.Id, c.Therapist);
        Assert.Single(history.Signatures);
        Assert.Equal(2, history.StatusChanges.Count);
    }

    [Fact]
    public async Task APtaSubmission_OpensACosignRequest_ThatCosigningResolves()
    {
        var c = Setup(ptaCosign: true);
        var pta = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = c.Org.Id, Role = UserRole.Assistant };
        var note = await DraftAsync(c, pta);
        await c.Notes.SignNoteAsync(note.Id, true, null, pta, "pw");

        var request = Assert.Single(c.Db.NoteCosignRequests.Where(r => r.NoteId == note.Id));
        Assert.Equal(CosignRequestStatus.Pending, request.Status);
        Assert.NotNull((await c.Db.ClinicalNotes.FindAsync(note.Id))!.SubmittedAt);

        await c.Notes.CosignNoteAsync(note.Id, c.Therapist, "pw");
        Assert.Equal(CosignRequestStatus.Cosigned, c.Db.NoteCosignRequests.Single(r => r.NoteId == note.Id).Status);
        Assert.Equal(new[] { SignatureMeaning.Author, SignatureMeaning.Cosign },
            c.Db.ElectronicSignatures.Where(s => s.NoteId == note.Id).OrderBy(s => s.SignedAt).Select(s => s.Meaning).ToArray());
    }

    [Fact]
    public async Task AnAmendmentsSignature_IsMarkedAsAnAmendment_AndTheOriginalsChangeIsRecorded()
    {
        var c = Setup();
        var original = await DraftAsync(c, c.Therapist);
        await c.Notes.SignNoteAsync(original.Id, true, null, c.Therapist, "pw");
        var amendment = await c.Notes.CreateAmendmentAsync(original.Id, new CreateAmendmentRequest("Wrong value"), c.Therapist);
        await c.Notes.SignNoteAsync(amendment.Id, true, null, c.Therapist, "pw");

        Assert.Equal(SignatureMeaning.Amendment, c.Db.ElectronicSignatures.Single(s => s.NoteId == amendment.Id).Meaning);
        Assert.Contains(c.Db.ClinicalNoteStatusChanges, s => s.NoteId == original.Id && s.ToStatus == NoteStatus.Amended && s.Reason == "Wrong value");
    }

    [Fact]
    public async Task ASignedNotesContentRows_CannotBeAddedChangedOrRemoved()
    {
        var c = Setup();
        var note = await DraftAsync(c, c.Therapist);
        var item = await c.Notes.AddInterventionAsync(note.Id, new CreateInterventionRequest("Bridges", null, null, 10, null, true, 0), c.Therapist);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw");

        var tracked = await c.Db.NoteInterventions.FindAsync(item.Id);
        tracked!.Minutes = 30;
        Assert.Throws<InvalidOperationException>(() => c.Db.SaveChanges());
        c.Db.ChangeTracker.Clear();

        c.Db.NoteInterventions.Remove((await c.Db.NoteInterventions.FindAsync(item.Id))!);
        Assert.Throws<InvalidOperationException>(() => c.Db.SaveChanges());
        c.Db.ChangeTracker.Clear();

        c.Db.ClinicalNoteFieldValues.Add(new ClinicalNoteFieldValue { NoteId = note.Id, FieldId = Guid.NewGuid(), FieldKey = "x", ValueText = "late" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task ADraftsContentRows_CanBeSaved()
    {
        var c = Setup();
        var note = await DraftAsync(c, c.Therapist);
        c.Db.ClinicalNoteFieldValues.Add(new ClinicalNoteFieldValue { NoteId = note.Id, FieldId = Guid.NewGuid(), FieldKey = "x", ValueNumber = 4 });
        await c.Db.SaveChangesAsync();
        Assert.Single(c.Db.ClinicalNoteFieldValues);
    }

    [Fact]
    public async Task OnlyAnUntouchedDraft_CanBeDeleted()
    {
        var c = Setup();
        var draft = await DraftAsync(c, c.Therapist);
        var signed = await DraftAsync(c, c.Therapist);
        await c.Notes.SignNoteAsync(signed.Id, true, null, c.Therapist, "pw");
        c.Db.ChangeTracker.Clear();

        c.Db.ClinicalNotes.Remove((await c.Db.ClinicalNotes.FindAsync(signed.Id))!);
        var ex = Assert.Throws<InvalidOperationException>(() => c.Db.SaveChanges());
        Assert.Contains("Void the note instead", ex.Message);
        c.Db.ChangeTracker.Clear();

        // A draft has no history rows that would block it at the database.
        var bare = new ClinicalNote { PatientId = c.Pat.Id, TherapistId = c.Therapist.UserId };
        c.Db.ClinicalNotes.Add(bare);
        await c.Db.SaveChangesAsync();
        c.Db.ClinicalNotes.Remove(bare);
        await c.Db.SaveChangesAsync();
        Assert.Null(await c.Db.ClinicalNotes.FindAsync(bare.Id));
        Assert.NotNull(await c.Db.ClinicalNotes.FindAsync(draft.Id));
    }

    [Fact]
    public async Task SignaturesAndStatusHistory_AreAppendOnly()
    {
        var c = Setup();
        var note = await DraftAsync(c, c.Therapist);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw");

        var signature = c.Db.ElectronicSignatures.Single();
        signature.SignerName = "Someone else";
        Assert.Throws<InvalidOperationException>(() => c.Db.SaveChanges());
        c.Db.ChangeTracker.Clear();

        c.Db.ClinicalNoteStatusChanges.Remove(c.Db.ClinicalNoteStatusChanges.First());
        Assert.Throws<InvalidOperationException>(() => c.Db.SaveChanges());
    }

    [Fact]
    public async Task APublishedTemplateVersion_CannotBeChanged()
    {
        var c = Setup();
        var template = new ClinicalNoteTemplate { Name = "Daily", NoteType = NoteType.Daily, Scope = TemplateScope.Platform };
        var version = new ClinicalNoteTemplateVersion { TemplateId = template.Id, VersionNumber = 1 };
        var section = new ClinicalNoteTemplateSection { VersionId = version.Id, Key = "s", Title = "Subjective" };
        var field = new ClinicalNoteTemplateField { VersionId = version.Id, SectionId = section.Id, Key = "pain", Label = "Pain", FieldType = TemplateFieldType.PainScale };
        c.Db.AddRange(template, version, section, field);
        await c.Db.SaveChangesAsync();

        field.Label = "Changed";
        Assert.Throws<InvalidOperationException>(() => c.Db.SaveChanges());
        c.Db.ChangeTracker.Clear();
        c.Db.ClinicalNoteTemplateSections.Remove((await c.Db.ClinicalNoteTemplateSections.FindAsync(section.Id))!);
        Assert.Throws<InvalidOperationException>(() => c.Db.SaveChanges());
    }

    [Fact]
    public async Task ASignedNote_CanOnlyBeVoided_ThroughTheVoidFields()
    {
        var c = Setup();
        var note = await DraftAsync(c, c.Therapist);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw");

        var tracked = (await c.Db.ClinicalNotes.FindAsync(note.Id))!;
        tracked.Status = NoteStatus.Voided;
        tracked.VoidReason = "Documented on the wrong patient";
        tracked.VoidedAt = DateTimeOffset.UtcNow;
        tracked.VoidedById = c.Admin.UserId;
        await c.Db.SaveChangesAsync();

        tracked.Objective = "changed after voiding";
        Assert.Throws<InvalidOperationException>(() => c.Db.SaveChanges());
    }

    [Fact]
    public async Task PlansOfCare_AreListedNewestFirst()
    {
        var c = Setup();
        var eval = await DraftAsync(c, c.Therapist);
        c.Db.PlansOfCare.AddRange(
            new PlanOfCare { PatientId = c.Pat.Id, SourceNoteId = eval.Id, Status = PlanOfCareStatus.Superseded, StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 8, 1) },
            new PlanOfCare { PatientId = c.Pat.Id, SourceNoteId = eval.Id, Status = PlanOfCareStatus.Active, StartDate = new DateOnly(2026, 8, 2), EndDate = new DateOnly(2026, 10, 2) });
        await c.Db.SaveChangesAsync();

        var plans = await c.Notes.ListPlansOfCareAsync(c.Pat.Id, c.Therapist);
        Assert.Equal(new[] { PlanOfCareStatus.Active, PlanOfCareStatus.Superseded }, plans.Select(p => p.Status).ToArray());
    }
}
