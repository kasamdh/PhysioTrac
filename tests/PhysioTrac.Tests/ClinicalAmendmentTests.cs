using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Documentation Phase 4: formal amendments (the signed original is
/// never changed), addenda validation, the per-note record, version history
/// names, and the documentation work queues.</summary>
public class ClinicalAmendmentTests
{
    private sealed record Ctx(PhysioTracDbContext Db, ClinicalNoteService Notes, Organization Org, Patient Pat, TestCurrentUser Therapist, TestCurrentUser Admin);

    private static Ctx Setup(bool ptaCosign = false)
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Org", Slug = "org", PtaCosignRequired = ptaCosign };
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Quinn", LastName = "Alvarez", DateOfBirth = new DateOnly(2001, 7, 30) };
        db.Organizations.Add(org);
        db.Patients.Add(pat);
        db.SaveChanges();
        var audit = new AuditService(db);
        var notes = new ClinicalNoteService(db, new TenantAccessService(db, audit), audit, new AcceptAnySignature());
        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist };
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        return new Ctx(db, notes, org, pat, therapist, admin);
    }

    private static async Task<ClinicalNote> SignedNoteAsync(Ctx c, TestCurrentUser who)
    {
        var note = await c.Notes.CreateDraftAsync(new CreateNoteRequest(
            c.Pat.Id, NoteType.Daily, new DateOnly(2026, 10, 6), null, "Knee sore", "Flexion 100", null, "Improving", "Continue",
            null, null, null, null, null, ObjectiveMeasurementsJson: """{"rom":[{"joint":"Knee","arom":"100"}]}"""), who);
        await c.Notes.AddInterventionAsync(note.Id, new CreateInterventionRequest("Quad sets", "Knee", InterventionCategory.TherapeuticExercise, 23, null, true, 0, "Tolerated"), who);
        return await c.Notes.SignNoteAsync(note.Id, true, null, who, "pw");
    }

    [Fact]
    public async Task Amendment_CopiesTheNote_AndSigningItSupersedesTheUnchangedOriginal()
    {
        var c = Setup();
        var original = await SignedNoteAsync(c, c.Therapist);
        var originalHash = original.SignatureHash;

        var amendment = await c.Notes.CreateAmendmentAsync(original.Id, new CreateAmendmentRequest("Wrong knee flexion value"), c.Therapist);
        Assert.Equal((NoteStatus.Draft, original.Id, "Wrong knee flexion value"), (amendment.Status, amendment.AmendsNoteId, amendment.AmendmentReason));
        Assert.Equal(("Knee sore", original.ObjectiveMeasurementsJson, original.ServiceDate), (amendment.Subjective, amendment.ObjectiveMeasurementsJson, amendment.ServiceDate));
        Assert.Null(amendment.AppointmentId);
        var copied = Assert.Single(await c.Notes.ListInterventionsAsync(amendment.Id, c.Therapist));
        Assert.Equal(("Quad sets", 23, "Tolerated"), (copied.Description, copied.Minutes, copied.PatientResponse));

        await c.Notes.UpdateDraftAsync(amendment.Id, new UpdateNoteRequest(null, "Flexion 110", null, null, null, null, null, null, null, null), c.Therapist);
        await c.Notes.SignNoteAsync(amendment.Id, true, null, c.Therapist, "pw");

        var reloaded = await c.Db.ClinicalNotes.AsNoTracking().FirstAsync(n => n.Id == original.Id);
        Assert.Equal(NoteStatus.Amended, reloaded.Status);
        Assert.Equal(("Flexion 100", originalHash), (reloaded.Objective, reloaded.SignatureHash));
        Assert.Contains(c.Db.AuditEvents, e => e.Action == "note.amended" && e.ObjectId == original.Id);
    }

    [Fact]
    public async Task Amend_ReturnsTheOpenAmendment_AndNeedsAReason()
    {
        var c = Setup();
        var original = await SignedNoteAsync(c, c.Therapist);

        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.CreateAmendmentAsync(original.Id, new CreateAmendmentRequest("  "), c.Therapist));
        var first = await c.Notes.CreateAmendmentAsync(original.Id, new CreateAmendmentRequest("Typo"), c.Therapist);
        var again = await c.Notes.CreateAmendmentAsync(original.Id, new CreateAmendmentRequest("Another"), c.Therapist);
        Assert.Equal(first.Id, again.Id);
    }

    [Fact]
    public async Task Amend_IsRefusedForDrafts_LockedNotes_AndOtherTherapists()
    {
        var c = Setup();
        var draft = await c.Notes.CreateDraftAsync(new CreateNoteRequest(
            c.Pat.Id, NoteType.Daily, new DateOnly(2026, 10, 6), null, null, null, null, null, null, null, null, null, null, null), c.Therapist);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.CreateAmendmentAsync(draft.Id, new CreateAmendmentRequest("x"), c.Therapist));

        var signed = await SignedNoteAsync(c, c.Therapist);
        var colleague = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = c.Org.Id, Role = UserRole.Therapist };
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.CreateAmendmentAsync(signed.Id, new CreateAmendmentRequest("x"), colleague));

        await c.Notes.LockNoteAsync(signed.Id, c.Admin);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.CreateAmendmentAsync(signed.Id, new CreateAmendmentRequest("x"), c.Therapist));
        Assert.Contains("locked", ex.Message);
    }

    [Fact]
    public async Task AnAmendmentCantBeSigned_IfTheOriginalWasLockedMeanwhile()
    {
        var c = Setup();
        var original = await SignedNoteAsync(c, c.Therapist);
        var amendment = await c.Notes.CreateAmendmentAsync(original.Id, new CreateAmendmentRequest("Typo"), c.Therapist);
        await c.Notes.LockNoteAsync(original.Id, c.Admin);

        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.SignNoteAsync(amendment.Id, true, null, c.Therapist, "pw"));
        Assert.Equal(NoteStatus.Draft, (await c.Db.ClinicalNotes.AsNoTracking().FirstAsync(n => n.Id == amendment.Id)).Status);
    }

    [Fact]
    public async Task AnAmendedNote_StaysImmutable()
    {
        var c = Setup();
        var original = await SignedNoteAsync(c, c.Therapist);
        var amendment = await c.Notes.CreateAmendmentAsync(original.Id, new CreateAmendmentRequest("Typo"), c.Therapist);
        await c.Notes.SignNoteAsync(amendment.Id, true, null, c.Therapist, "pw");

        await Assert.ThrowsAnyAsync<Exception>(() => c.Notes.UpdateDraftAsync(original.Id,
            new UpdateNoteRequest("changed", null, null, null, null, null, null, null, null, null), c.Admin));
        await Assert.ThrowsAnyAsync<Exception>(() => c.Notes.CreateAddendumAsync(original.Id, new CreateAddendumRequest("r", "b"), c.Therapist));
    }

    [Fact]
    public async Task APtasAmendment_SupersedesTheOriginalOnlyWhenCosigned()
    {
        var c = Setup(ptaCosign: true);
        var pta = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = c.Org.Id, Role = UserRole.Assistant };
        var original = await SignedNoteAsync(c, pta);
        await c.Notes.CosignNoteAsync(original.Id, c.Therapist, "pw");

        var amendment = await c.Notes.CreateAmendmentAsync(original.Id, new CreateAmendmentRequest("Typo"), pta);
        var submitted = await c.Notes.SignNoteAsync(amendment.Id, true, null, pta, "pw");
        Assert.Equal(NoteStatus.ReviewRequired, submitted.Status);
        Assert.Equal(NoteStatus.Signed, (await c.Db.ClinicalNotes.AsNoTracking().FirstAsync(n => n.Id == original.Id)).Status);

        await c.Notes.CosignNoteAsync(amendment.Id, c.Therapist, "pw");
        Assert.Equal(NoteStatus.Amended, (await c.Db.ClinicalNotes.AsNoTracking().FirstAsync(n => n.Id == original.Id)).Status);
    }

    [Fact]
    public async Task Addendum_NeedsReasonAndText()
    {
        var c = Setup();
        var signed = await SignedNoteAsync(c, c.Therapist);

        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.CreateAddendumAsync(signed.Id, new CreateAddendumRequest("", "text"), c.Therapist));
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.CreateAddendumAsync(signed.Id, new CreateAddendumRequest("Late entry", " "), c.Therapist));
        var added = await c.Notes.CreateAddendumAsync(signed.Id, new CreateAddendumRequest(" Late entry ", "Called patient re HEP."), c.Therapist);
        Assert.Equal("Late entry", added.Reason);
    }

    [Fact]
    public async Task Record_ListsActions_Addenda_AndTheAmendmentLink()
    {
        var c = Setup();
        var signed = await SignedNoteAsync(c, c.Therapist);
        await c.Notes.CreateAddendumAsync(signed.Id, new CreateAddendumRequest("Late entry", "Called patient."), c.Therapist);

        var forAuthor = await c.Notes.GetNoteRecordAsync(signed.Id, c.Therapist);
        Assert.Equal(new NoteActionsDto(false, false, false, true, true, false, CanVoid: true, VoidNeedsPassword: true), forAuthor.Actions);
        Assert.Equal("Late entry", Assert.Single(forAuthor.Addenda).Reason);
        Assert.True((await c.Notes.GetNoteRecordAsync(signed.Id, c.Admin)).Actions.CanLock);

        var amendment = await c.Notes.CreateAmendmentAsync(signed.Id, new CreateAmendmentRequest("Typo"), c.Therapist);
        var record = await c.Notes.GetNoteRecordAsync(signed.Id, c.Therapist);
        Assert.Equal((amendment.Id, NoteStatus.Draft), (record.AmendmentNoteId, record.AmendmentStatus));
    }

    [Fact]
    public async Task AColleague_CanReadAndCosign_APtasNote()
    {
        var c = Setup(ptaCosign: true);
        var pta = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = c.Org.Id, Role = UserRole.Assistant };
        var note = await SignedNoteAsync(c, pta);

        var record = await c.Notes.GetNoteRecordAsync(note.Id, c.Therapist);
        Assert.True(record.Actions.CanCosign);
        Assert.False((await c.Notes.GetNoteRecordAsync(note.Id, pta)).Actions.CanCosign);
    }

    [Fact]
    public async Task Queues_ShowMyUnsignedNotes_AndNotesAwaitingMyCosign()
    {
        var c = Setup(ptaCosign: true);
        var pta = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = c.Org.Id, Role = UserRole.Assistant };
        var awaiting = await SignedNoteAsync(c, pta);
        var myDraft = await c.Notes.CreateDraftAsync(new CreateNoteRequest(
            c.Pat.Id, NoteType.Daily, new DateOnly(2026, 10, 5), null, null, null, null, null, null, null, null, null, null, null), c.Admin);
        await SignedNoteAsync(c, c.Admin); // signed: in no queue

        var q = await c.Notes.GetNoteQueuesAsync(c.Admin);
        var mine = Assert.Single(q.MyUnsignedNotes);
        Assert.Equal((myDraft.Id, "Quinn Alvarez"), (mine.NoteId, mine.PatientName));
        Assert.Equal(awaiting.Id, Assert.Single(q.AwaitingMyCosign).NoteId);

        var ptaQueues = await c.Notes.GetNoteQueuesAsync(pta);
        Assert.Equal(awaiting.Id, Assert.Single(ptaQueues.MyUnsignedNotes).NoteId);
        Assert.Empty(ptaQueues.AwaitingMyCosign);
    }
}
