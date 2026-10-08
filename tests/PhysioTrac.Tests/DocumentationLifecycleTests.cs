using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Seed;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Accepts only the password "pw".</summary>
public sealed class OnlyPasswordPw : ISignatureVerifier
{
    public Task VerifyAsync(Guid userId, string? password, CancellationToken ct = default) =>
        password == "pw" ? Task.CompletedTask : throw new SignatureVerificationException("The password is incorrect.");
}

/// <summary>The documentation lifecycle: the PTA submit / PT review / cosign
/// or return workflow, signing rules and signature records, voiding,
/// locking, amendments, authorization and stale-copy (concurrency) checks.</summary>
public class DocumentationLifecycleTests
{
    private sealed record Ctx(PhysioTracDbContext Db, ClinicalNoteService Notes, Organization Org, Patient Pat,
        TestCurrentUser Pta, TestCurrentUser Pt, TestCurrentUser OtherPt, TestCurrentUser OtherPta, TestCurrentUser Admin);

    private static async Task<Ctx> SetupAsync(bool ptaCosignPolicy = true)
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional Clinic", Slug = "fictional", PtaCosignRequired = ptaCosignPolicy };
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Taylor", LastName = "Sample", DateOfBirth = new DateOnly(1975, 6, 6) };
        db.Organizations.Add(org);
        db.Patients.Add(pat);
        TestCurrentUser User(UserRole role, string first, string? credential)
        {
            var u = new ApplicationUser { UserName = first.ToLowerInvariant(), FirstName = first, LastName = "Example", OrganizationId = org.Id, Role = role, Credential = credential };
            db.Users.Add(u);
            return new TestCurrentUser { UserId = u.Id, OrganizationId = org.Id, Role = role };
        }
        var pta = User(UserRole.Assistant, "Riley", "PTA");
        var pt = User(UserRole.Therapist, "Jordan", "PT, DPT");
        var otherPt = User(UserRole.Therapist, "Casey", "PT");
        var otherPta = User(UserRole.Assistant, "Morgan", "PTA");
        var admin = User(UserRole.Admin, "Avery", null);
        await db.SaveChangesAsync();
        await SystemTemplateSeeder.SeedAsync(db);
        var audit = new AuditService(db);
        return new Ctx(db, new ClinicalNoteService(db, new TenantAccessService(db, audit), audit, new OnlyPasswordPw()),
            org, pat, pta, pt, otherPt, otherPta, admin);
    }

    private static async Task<ClinicalNote> CompletedNoteAsync(Ctx c, TestCurrentUser author, NoteType type = NoteType.Daily, DateOnly? date = null)
    {
        var note = await c.Notes.CreateDraftAsync(new CreateNoteRequest(c.Pat.Id, type, date ?? new DateOnly(2026, 10, 7), null,
            null, null, null, null, null, null, null, null, null, null), author);
        if (type == NoteType.Daily)
        {
            await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(await VersionAsync(c, note, author),
                [new("patientResponse", Text: "Tolerated"), new("continuedSkilledNeed", Text: "Yes")],
                Subjective: "Less pain.", Objective: "Knee flexion 110 deg.", Assessment: "Improving.", Plan: "Continue."), author);
        }
        return note;
    }

    private static async Task<int> VersionAsync(Ctx c, ClinicalNote note, TestCurrentUser who) =>
        (await c.Notes.GetEncounterAsync(note.Id, who)).SaveVersion;

    private ClinicalNote Reload(Ctx c, Guid id) => c.Db.ClinicalNotes.AsNoTracking().Single(n => n.Id == id);

    [Fact]
    public async Task APtaSubmission_GoesToCosign_WithAFullSignatureRecord()
    {
        var c = await SetupAsync();
        var note = await CompletedNoteAsync(c, c.Pta);
        var version = await VersionAsync(c, note, c.Pta);
        Assert.True((await c.Notes.GetNoteRecordAsync(note.Id, c.Pta)).Actions.SignSubmitsForCosign);

        await c.Notes.SignNoteAsync(note.Id, true, "10.0.0.1", c.Pta, "pw", version);
        var submitted = Reload(c, note.Id);
        Assert.Equal((NoteStatus.ReviewRequired, DocumentationStatus.CosignRequired), (submitted.Status, ClinicalNoteMapper.ToDto(submitted).DocumentationStatus));
        Assert.Equal(CosignRequestStatus.Pending, c.Db.NoteCosignRequests.Single().Status);

        var sig = c.Db.ElectronicSignatures.Single();
        Assert.Equal(("Riley Example", "PTA", "Assistant", SignatureMeaning.Author, "America/New_York", version + 1),
            (sig.SignerName, sig.Credentials, sig.Role, sig.Meaning, sig.DisplayTimeZone, sig.NoteVersionNumber));
        Assert.Equal(TimeSpan.Zero, sig.SignedAt.Offset);
        var history = await c.Notes.GetNoteHistoryAsync(note.Id, c.Pta);
        Assert.EndsWith("(America/New_York)", history.Signatures.Single().LocalSignedAt);
        Assert.Contains(c.Db.AuditEvents, e => e.Action == "note.submitted_for_cosign" && e.MetadataJson!.Contains("organization_policy"));

        // Submitted documentation can't be edited.
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.SaveEncounterAsync(note.Id,
            new SaveEncounterRequest(version + 1, Subjective: "changed"), c.Pta));
    }

    [Fact]
    public async Task TheSystemDecidesWhetherACosignIsRequired()
    {
        var c = await SetupAsync(ptaCosignPolicy: false);
        var daily = await CompletedNoteAsync(c, c.Pta);
        await c.Notes.SignNoteAsync(daily.Id, true, null, c.Pta, "pw");
        Assert.Equal(NoteStatus.Signed, Reload(c, daily.Id).Status); // no policy: a PTA's daily note is final

        // A PT-only note type written by a PTA always needs a PT.
        Assert.True(LifecycleRules.IsPtOnly(NoteType.Progress));
        Assert.Equal("note_type", LifecycleRules.CosignReason(UserRole.Assistant, NoteType.Discharge, false));
        Assert.Equal("organization_policy", LifecycleRules.CosignReason(UserRole.Assistant, NoteType.Daily, true));
        Assert.Null(LifecycleRules.CosignReason(UserRole.Therapist, NoteType.Discharge, true));
        var progress = await CompletedNoteAsync(c, c.Pta, NoteType.Progress);
        Assert.True((await c.Notes.GetNoteRecordAsync(progress.Id, c.Pta)).Actions.SignSubmitsForCosign);

        // A PT's own note is final when signed.
        var ptNote = await CompletedNoteAsync(c, c.Pt);
        await c.Notes.SignNoteAsync(ptNote.Id, true, null, c.Pt, "pw");
        Assert.Equal(NoteStatus.Signed, Reload(c, ptNote.Id).Status);
    }

    [Fact]
    public async Task APt_ReviewsAndCosigns()
    {
        var c = await SetupAsync();
        var note = await CompletedNoteAsync(c, c.Pta);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Pta, "pw");

        var record = await c.Notes.GetNoteRecordAsync(note.Id, c.Pt);
        Assert.True(record.Actions.CanStartReview && record.Actions.CanCosign && record.Actions.CanReturn);
        await c.Notes.StartReviewAsync(note.Id, c.Pt);
        Assert.Equal(DocumentationStatus.InReview, ClinicalNoteMapper.ToDto(Reload(c, note.Id)).DocumentationStatus);

        await Assert.ThrowsAsync<SignatureVerificationException>(() => c.Notes.CosignNoteAsync(note.Id, c.Pt, "wrong"));
        await c.Notes.CosignNoteAsync(note.Id, c.Pt, "pw");
        var cosigned = Reload(c, note.Id);
        Assert.Equal((NoteStatus.Signed, DocumentationStatus.Cosigned, c.Pt.UserId),
            (cosigned.Status, ClinicalNoteMapper.ToDto(cosigned).DocumentationStatus, cosigned.CosignedById!.Value));
        Assert.Equal([SignatureMeaning.Author, SignatureMeaning.Cosign], c.Db.ElectronicSignatures.OrderBy(s => s.SignedAt).Select(s => s.Meaning));
        Assert.Equal([NoteStatus.Draft, NoteStatus.ReviewRequired, NoteStatus.InReview, NoteStatus.Signed],
            c.Db.ClinicalNoteStatusChanges.Where(s => s.NoteId == note.Id).OrderBy(s => s.CreatedAt).Select(s => s.ToStatus));
        Assert.Contains(c.Db.AuditEvents, e => e.Action == "note.review_started");
        Assert.Contains(c.Db.AuditEvents, e => e.Action == "note.cosigned");
    }

    [Fact]
    public async Task APt_ReturnsANoteForCorrection_AndThePtaResubmits()
    {
        var c = await SetupAsync();
        var note = await CompletedNoteAsync(c, c.Pta);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Pta, "pw");
        await c.Notes.StartReviewAsync(note.Id, c.Pt);

        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.ReturnForCorrectionAsync(note.Id, "  ", c.Pt));
        await c.Notes.ReturnForCorrectionAsync(note.Id, "Add the patient's response to gait training.", c.Pt);
        var returned = Reload(c, note.Id);
        Assert.Equal((NoteStatus.ReturnedForCorrection, "Add the patient's response to gait training."), (returned.Status, returned.ReturnReason));
        Assert.Null(returned.SignedAt);
        Assert.Null(returned.SignatureHash);
        Assert.Equal(CosignRequestStatus.Returned, c.Db.NoteCosignRequests.Single().Status);
        Assert.Equal("Add the patient's response to gait training.",
            c.Db.ClinicalNoteStatusChanges.Single(s => s.ToStatus == NoteStatus.ReturnedForCorrection).Reason);

        // The PTA corrects and resubmits; the first signature stays in the history.
        var version = await VersionAsync(c, note, c.Pta);
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(version, Objective: "Knee flexion 110 deg; gait steady."), c.Pta);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Pta, "pw");
        var resubmitted = Reload(c, note.Id);
        Assert.Equal((NoteStatus.ReviewRequired, (string?)null), (resubmitted.Status, resubmitted.ReturnReason));
        Assert.Equal(2, c.Db.ElectronicSignatures.Count(s => s.Meaning == SignatureMeaning.Author));
        Assert.Equal(2, c.Db.NoteCosignRequests.Count());
        Assert.Contains(c.Db.AuditEvents, e => e.Action == "note.returned_for_correction");
    }

    [Fact]
    public async Task OnlyAuthorizedPeopleSignReviewAndCosign()
    {
        var c = await SetupAsync();
        var note = await CompletedNoteAsync(c, c.Pta);
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.SignNoteAsync(note.Id, true, null, c.OtherPta, "pw"));
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.SignNoteAsync(note.Id, true, null, c.Pt, "pw"));
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Pta, "pw");

        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.CosignNoteAsync(note.Id, c.Pta, "pw"));
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.CosignNoteAsync(note.Id, c.OtherPta, "pw"));
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.ReturnForCorrectionAsync(note.Id, "x", c.Pta));
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.StartReviewAsync(note.Id, c.OtherPta));
        var ptaView = (await c.Notes.GetNoteRecordAsync(note.Id, c.Pta)).Actions;
        Assert.False(ptaView.CanCosign || ptaView.CanReturn || ptaView.CanStartReview || ptaView.CanVoid);

        // A PT can't cosign their own note.
        var ptNote = await CompletedNoteAsync(c, c.Pt);
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.CosignNoteAsync(ptNote.Id, c.Pt, "pw"));
        // Signed notes can't be signed again.
        await c.Notes.CosignNoteAsync(note.Id, c.OtherPt, "pw");
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.SignNoteAsync(note.Id, true, null, c.Admin, "pw"));
    }

    [Fact]
    public async Task SigningIsRefused_WhenRequiredFieldsAreEmpty_OrTheCopyIsOutOfDate()
    {
        var c = await SetupAsync(ptaCosignPolicy: false);
        var empty = await c.Notes.CreateDraftAsync(new CreateNoteRequest(c.Pat.Id, NoteType.Daily, new DateOnly(2026, 10, 7), null,
            null, null, null, null, null, null, null, null, null, null), c.Pt);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.SignNoteAsync(empty.Id, true, null, c.Pt, "pw"));
        Assert.Contains("missing_required_fields", ex.Message);

        var note = await CompletedNoteAsync(c, c.Pt);
        var stale = await VersionAsync(c, note, c.Pt);
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(stale, Plan: "Continue; add balance work."), c.Admin);
        await Assert.ThrowsAsync<EncounterConflictException>(() => c.Notes.SignNoteAsync(note.Id, true, null, c.Pt, "pw", stale));
        Assert.Equal(NoteStatus.Draft, Reload(c, note.Id).Status);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Pt, "pw", stale + 1);
        Assert.Equal(NoteStatus.Signed, Reload(c, note.Id).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.SignNoteAsync(note.Id, false, null, c.Pt, "pw"));
    }

    [Fact]
    public async Task VoidingADraft_NeedsAReason_AndFreesItsVisit()
    {
        var c = await SetupAsync(ptaCosignPolicy: false);
        var at = new DateTimeOffset(2026, 10, 7, 14, 0, 0, TimeSpan.Zero);
        var appointment = new Appointment { PatientId = c.Pat.Id, TherapistId = c.Pt.UserId, StartsAt = at, EndsAt = at.AddHours(1) };
        c.Db.Appointments.Add(appointment);
        await c.Db.SaveChangesAsync();
        var note = (await c.Notes.OpenAppointmentEncounterAsync(appointment.Id, c.Pt));

        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.VoidNoteAsync(note.NoteId, new VoidNoteRequest(""), c.Pt));
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.VoidNoteAsync(note.NoteId, new VoidNoteRequest("Wrong patient"), c.OtherPt));
        await c.Notes.VoidNoteAsync(note.NoteId, new VoidNoteRequest("Started on the wrong patient."), c.Pt);

        var voided = Reload(c, note.NoteId);
        Assert.Equal((NoteStatus.Voided, "Started on the wrong patient.", c.Pt.UserId, (Guid?)null),
            (voided.Status, voided.VoidReason, voided.VoidedById!.Value, voided.AppointmentId));
        Assert.Empty(c.Db.ElectronicSignatures); // a draft needs no signature to void
        var reopened = await c.Notes.OpenAppointmentEncounterAsync(appointment.Id, c.Pt);
        Assert.NotEqual(note.NoteId, reopened.NoteId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.VoidNoteAsync(note.NoteId, new VoidNoteRequest("again"), c.Admin));
    }

    [Fact]
    public async Task VoidingASignedNote_NeedsAPassword_KeepsItsContent_AndRestoresThePreviousPlan()
    {
        var c = await SetupAsync(ptaCosignPolicy: false);
        var note = await CompletedNoteAsync(c, c.Pt);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Pt, "pw");
        var hash = Reload(c, note.Id).SignatureHash;
        // Plans of care: an earlier one superseded by a plan this note created.
        var earlier = new PlanOfCare { PatientId = c.Pat.Id, SourceNoteId = note.Id, Status = PlanOfCareStatus.Superseded, StartDate = new DateOnly(2026, 8, 1), EndDate = new DateOnly(2026, 9, 30) };
        var current = new PlanOfCare { PatientId = c.Pat.Id, SourceNoteId = note.Id, Status = PlanOfCareStatus.Active, StartDate = new DateOnly(2026, 10, 7), EndDate = new DateOnly(2026, 12, 1), PreviousPlanOfCareId = earlier.Id };
        earlier.SourceNoteId = (await CompletedNoteAsync(c, c.Pt, date: new DateOnly(2026, 8, 1))).Id;
        c.Db.PlansOfCare.AddRange(earlier, current);
        await c.Db.SaveChangesAsync();

        Assert.True((await c.Notes.GetNoteRecordAsync(note.Id, c.Pt)).Actions.VoidNeedsPassword);
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.VoidNoteAsync(note.Id, new VoidNoteRequest("x", "pw"), c.OtherPt));
        await Assert.ThrowsAsync<SignatureVerificationException>(() => c.Notes.VoidNoteAsync(note.Id, new VoidNoteRequest("Documented on the wrong date.", "nope"), c.Pt));
        await c.Notes.VoidNoteAsync(note.Id, new VoidNoteRequest("Documented on the wrong date.", "pw"), c.Pt);

        var voided = Reload(c, note.Id);
        Assert.Equal((NoteStatus.Voided, hash, "Continue."), (voided.Status, voided.SignatureHash, voided.Plan));
        Assert.Equal(SignatureMeaning.Void, c.Db.ElectronicSignatures.OrderBy(s => s.SignedAt).Last().Meaning);
        Assert.Equal((PlanOfCareStatus.Voided, PlanOfCareStatus.Active),
            (c.Db.PlansOfCare.Single(p => p.Id == current.Id).Status, c.Db.PlansOfCare.Single(p => p.Id == earlier.Id).Status));
        Assert.Contains(c.Db.AuditEvents, e => e.Action == "note.voided");
        Assert.Contains(c.Db.ClinicalNoteVersions, v => v.NoteId == note.Id && v.ContentJson.Contains("\"Status\":6"));
    }

    [Fact]
    public async Task NotesUnderReview_CantBeVoided_AndLockingIsForAdmins()
    {
        var c = await SetupAsync();
        var note = await CompletedNoteAsync(c, c.Pta);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Pta, "pw");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.VoidNoteAsync(note.Id, new VoidNoteRequest("x", "pw"), c.Admin));
        Assert.Contains("Return the note", ex.Message);

        await c.Notes.CosignNoteAsync(note.Id, c.Pt, "pw");
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.LockNoteAsync(note.Id, c.Pt));
        await c.Notes.LockNoteAsync(note.Id, c.Admin);
        Assert.Equal(DocumentationStatus.Locked, ClinicalNoteMapper.ToDto(Reload(c, note.Id)).DocumentationStatus);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => c.Notes.CreateAmendmentAsync(note.Id, new CreateAmendmentRequest("late correction"), c.Admin));
        await c.Notes.VoidNoteAsync(note.Id, new VoidNoteRequest("Duplicate of another visit's note.", "pw"), c.Admin);
        Assert.Equal(NoteStatus.Voided, Reload(c, note.Id).Status);
    }

    [Fact]
    public async Task Amendments_NeedAReason_AndKeepTheOriginal()
    {
        var c = await SetupAsync(ptaCosignPolicy: false);
        var note = await CompletedNoteAsync(c, c.Pt);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Pt, "pw");
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.CreateAmendmentAsync(note.Id, new CreateAmendmentRequest(" "), c.Pt));

        var amendment = await c.Notes.CreateAmendmentAsync(note.Id, new CreateAmendmentRequest("Wrong knee documented."), c.Pt);
        await c.Notes.SaveEncounterAsync(amendment.Id, new SaveEncounterRequest(await VersionAsync(c, amendment, c.Pt),
            Objective: "Left knee flexion 110 deg."), c.Pt);
        await c.Notes.SignNoteAsync(amendment.Id, true, null, c.Pt, "pw");

        var original = Reload(c, note.Id);
        Assert.Equal((NoteStatus.Amended, "Knee flexion 110 deg."), (original.Status, original.Objective));
        Assert.Equal(DocumentationStatus.Amended, ClinicalNoteMapper.ToDto(original).DocumentationStatus);
        Assert.Equal(SignatureMeaning.Amendment, c.Db.ElectronicSignatures.Single(s => s.NoteId == amendment.Id).Meaning);
        // A signed amendment is corrected by amending again, not voided.
        Assert.False((await c.Notes.GetNoteRecordAsync(amendment.Id, c.Pt)).Actions.CanVoid);
    }

    [Fact]
    public void DocumentationStatuses_CoverTheLifecycle()
    {
        Assert.Equal(DocumentationStatus.ReadyToSign, LifecycleRules.For(NoteStatus.Draft, false, readyToSign: true));
        Assert.Equal(DocumentationStatus.Draft, LifecycleRules.For(NoteStatus.Draft, false));
        Assert.Equal(DocumentationStatus.ReturnedForCorrection, LifecycleRules.For(NoteStatus.ReturnedForCorrection, false));
        Assert.Equal(DocumentationStatus.CosignRequired, LifecycleRules.For(NoteStatus.ReviewRequired, false));
        Assert.Equal(DocumentationStatus.Cosigned, LifecycleRules.For(NoteStatus.Signed, true));
        Assert.Equal(DocumentationStatus.Voided, LifecycleRules.For(NoteStatus.Voided, false));
        Assert.Equal(11, Enum.GetValues<DocumentationStatus>().Length);
    }
}
