using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;
using Xunit;

namespace PhysioTrac.Tests;

/// <summary>Mirrors the original's `DocumentationApiTests`/`PtaCosignTests`
/// intent: signed-note immutability, addendum-only-on-signed-notes,
/// intervention-lock-on-sign, and PTA cosign routing.</summary>
public class ClinicalNoteServiceTests
{
    private static (PhysioTracDbContext Db, ClinicalNoteService Service, Organization Org, Patient Patient)
        NewService(bool ptaCosignRequired = true)
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000, PtaCosignRequired = ptaCosignRequired };
        var patient = new Patient { OrganizationId = org.Id, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Organizations.Add(org);
        db.Patients.Add(patient);
        db.SaveChanges();

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var service = new ClinicalNoteService(db, tenantAccess, audit);
        return (db, service, org, patient);
    }

    private static TestCurrentUser Therapist(Guid orgId, Guid? userId = null) => new()
    {
        UserId = userId ?? Guid.NewGuid(),
        OrganizationId = orgId,
        Role = UserRole.Therapist,
    };

    private static TestCurrentUser Assistant(Guid orgId, Guid? userId = null) => new()
    {
        UserId = userId ?? Guid.NewGuid(),
        OrganizationId = orgId,
        Role = UserRole.Assistant,
    };

    private static CreateNoteRequest CompleteDailyNote(Guid patientId) => new(
        patientId, NoteType.Daily, DateOnly.FromDateTime(DateTime.UtcNow), null,
        "Patient reports improvement", "5/5 strength all extremities", "Therapeutic exercise x30min",
        "Patient tolerated treatment well", "Continue plan of care", null, null, null, null, null);

    [Fact]
    public async Task SignNote_WithCompleteDocumentation_Succeeds()
    {
        var (_, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), therapist);

        var signed = await service.SignNoteAsync(note.Id, true, null, therapist);

        Assert.Equal(NoteStatus.Signed, signed.Status);
        Assert.NotNull(signed.SignedAt);
    }

    [Fact]
    public async Task SignNote_MissingObjective_IsBlocked()
    {
        var (_, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var request = CompleteDailyNote(patient.Id) with { Objective = "" };
        var note = await service.CreateDraftAsync(request, therapist);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SignNoteAsync(note.Id, true, null, therapist));
    }

    [Fact]
    public async Task SignNote_WithoutAttestation_Throws()
    {
        var (_, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), therapist);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SignNoteAsync(note.Id, false, null, therapist));
    }

    [Fact]
    public async Task SignedNote_CannotBeEditedDirectly()
    {
        // CanEditNote already returns false once signed, so this surfaces
        // as a permission denial at the service layer — the DbContext's
        // own append-only-once-signed enforcement (EnforceSignedNoteImmutability)
        // is a second, defense-in-depth backstop that a direct EF write
        // bypassing this service would still hit.
        var (db, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), therapist);
        await service.SignNoteAsync(note.Id, true, null, therapist);

        await Assert.ThrowsAsync<PhysioTrac.Application.Common.ForbiddenException>(() =>
            service.UpdateDraftAsync(note.Id, new UpdateNoteRequest("tampered", null, null, null, null, null, null, null, null, null), therapist));
    }

    [Fact]
    public async Task SignedNote_DirectDbWrite_IsBlockedByAppendOnlyEnforcement()
    {
        var (db, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), therapist);
        await service.SignNoteAsync(note.Id, true, null, therapist);

        var tracked = await db.ClinicalNotes.FirstAsync(n => n.Id == note.Id);
        tracked.Subjective = "tampered";

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Addendum_CanOnlyBeCreatedOnSignedNote()
    {
        // Matches the original: the API view checks `can_create_addendum`
        // (which already folds in "note.is_signed") before ever reaching the
        // service function's own defensive re-check — so the not-yet-signed
        // case surfaces as a permission denial, not a 409 domain error.
        var (_, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), therapist);

        await Assert.ThrowsAsync<PhysioTrac.Application.Common.ForbiddenException>(() =>
            service.CreateAddendumAsync(note.Id, new CreateAddendumRequest("correction", "body"), therapist));

        await service.SignNoteAsync(note.Id, true, null, therapist);
        var addendum = await service.CreateAddendumAsync(note.Id, new CreateAddendumRequest("correction", "body"), therapist);
        Assert.Equal(note.Id, addendum.NoteId);
    }

    [Fact]
    public async Task Intervention_CannotBeAddedOnceSigned()
    {
        // Matches the original: `interventions_replace` checks `can_edit_note`
        // (false once signed) before ever reaching NoteIntervention.clean()'s
        // own "signed note" ValidationError — so this surfaces as a
        // permission denial, not a 409 domain error.
        var (_, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), therapist);
        await service.SignNoteAsync(note.Id, true, null, therapist);

        await Assert.ThrowsAsync<PhysioTrac.Application.Common.ForbiddenException>(() =>
            service.AddInterventionAsync(note.Id, new CreateInterventionRequest("Gait training", null, null, 15, null, true, 0), therapist));
    }

    [Fact]
    public async Task PtaAuthoredNote_WithOrgCosignPolicy_RoutesToReviewRequired()
    {
        var (_, service, org, patient) = NewService(ptaCosignRequired: true);
        var assistant = Assistant(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), assistant);

        var signed = await service.SignNoteAsync(note.Id, true, null, assistant);

        Assert.Equal(NoteStatus.ReviewRequired, signed.Status);
        Assert.True(note.CosignRequired);
    }

    [Fact]
    public async Task PtaAuthoredNote_WithOrgCosignOptOut_SignsDirectly()
    {
        var (_, service, org, patient) = NewService(ptaCosignRequired: false);
        var assistant = Assistant(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), assistant);

        var signed = await service.SignNoteAsync(note.Id, true, null, assistant);

        Assert.Equal(NoteStatus.Signed, signed.Status);
    }

    [Fact]
    public async Task Cosign_BySupervisingTherapist_CompletesNote()
    {
        var (_, service, org, patient) = NewService(ptaCosignRequired: true);
        var assistant = Assistant(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), assistant);
        await service.SignNoteAsync(note.Id, true, null, assistant);

        var supervisor = Therapist(org.Id);
        var cosigned = await service.CosignNoteAsync(note.Id, supervisor);

        Assert.Equal(NoteStatus.Signed, cosigned.Status);
        Assert.Equal(supervisor.UserId, cosigned.CosignedById);
    }

    [Fact]
    public async Task Cosign_ByOwnAuthor_IsRejected()
    {
        var (_, service, org, patient) = NewService(ptaCosignRequired: true);
        var assistantId = Guid.NewGuid();
        var assistant = Assistant(org.Id, assistantId);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), assistant);
        await service.SignNoteAsync(note.Id, true, null, assistant);

        // Re-fetch as the same user attempting to cosign their own note.
        Assert.False(service.CanCosignNote(assistant, note));
    }

    [Fact]
    public async Task CrossOrganizationNoteAccess_IsRejected()
    {
        var (_, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), therapist);

        var otherOrgUser = Therapist(Guid.NewGuid());
        await Assert.ThrowsAsync<PhysioTrac.Application.Common.ForbiddenException>(() =>
            service.GetAsync(note.Id, otherOrgUser));
    }
}
