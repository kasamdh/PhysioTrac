using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Immutable version history and e-signature (credentials/IP/hash),
/// plus the new Locked status -- the pieces of "Phase 5A" that extend the
/// already-thoroughly-tested Draft/Signed lifecycle in
/// ClinicalNoteServiceTests.</summary>
public class ClinicalNoteVersionAndSignatureTests
{
    private static (PhysioTracDbContext Db, ClinicalNoteService Service, Organization Org, Patient Patient)
        NewService()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000, PtaCosignRequired = false };
        var patient = new Patient { OrganizationId = org.Id, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Organizations.Add(org);
        db.Patients.Add(patient);
        db.SaveChanges();

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var service = new ClinicalNoteService(db, tenantAccess, audit);
        return (db, service, org, patient);
    }

    private static TestCurrentUser Therapist(Guid orgId) => new() { UserId = Guid.NewGuid(), OrganizationId = orgId, Role = UserRole.Therapist };
    private static TestCurrentUser Admin(Guid orgId) => new() { UserId = Guid.NewGuid(), OrganizationId = orgId, Role = UserRole.Admin };

    private static CreateNoteRequest CompleteDailyNote(Guid patientId) => new(
        patientId, NoteType.Daily, DateOnly.FromDateTime(DateTime.UtcNow), null,
        "Patient reports improvement", "5/5 strength all extremities", "Therapeutic exercise x30min",
        "Patient tolerated treatment well", "Continue plan of care", null, null, null, null, null);

    [Fact]
    public async Task CreateDraft_WritesVersion1()
    {
        var (db, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), therapist);

        var versions = await service.GetVersionHistoryAsync(note.Id, therapist);
        var only = Assert.Single(versions);
        Assert.Equal(1, only.VersionNumber);
        Assert.False(only.IsSignedVersion);
    }

    [Fact]
    public async Task UpdateDraft_WritesANewVersion_EachCall_SupportingRepeatedAutosave()
    {
        var (db, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), therapist);

        await service.UpdateDraftAsync(note.Id, new UpdateNoteRequest("Updated subjective 1", null, null, null, null, null, null, null, null, null), therapist);
        await service.UpdateDraftAsync(note.Id, new UpdateNoteRequest("Updated subjective 2", null, null, null, null, null, null, null, null, null), therapist);

        var versions = await service.GetVersionHistoryAsync(note.Id, therapist);
        Assert.Equal(3, versions.Count); // create + 2 autosaves
        Assert.Equal(new[] { 1, 2, 3 }, versions.Select(v => v.VersionNumber));
        Assert.Contains("Updated subjective 2", versions.Last().ContentJson);
    }

    [Fact]
    public async Task SignNote_CapturesCredentialsIpAddressAndAContentHash()
    {
        var (db, service, org, patient) = NewService();
        var therapistUserId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser { Id = therapistUserId, UserName = "jamie", FirstName = "Jamie", LastName = "Chen", Credential = "PT, DPT", OrganizationId = org.Id, Role = UserRole.Therapist });
        await db.SaveChangesAsync();
        var therapist = new TestCurrentUser { UserId = therapistUserId, OrganizationId = org.Id, Role = UserRole.Therapist };

        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), therapist);
        var signed = await service.SignNoteAsync(note.Id, true, "203.0.113.42", therapist);

        Assert.Equal("PT, DPT", signed.SignatureCredentials);
        Assert.Equal("203.0.113.42", signed.SignatureIpAddress);
        Assert.False(string.IsNullOrEmpty(signed.SignatureHash));
        Assert.Equal(64, signed.SignatureHash!.Length); // SHA-256 hex digest
    }

    [Fact]
    public async Task SignNote_WritesTheFinalSignedVersionSnapshot()
    {
        var (db, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), therapist);

        await service.SignNoteAsync(note.Id, true, null, therapist);

        var versions = await service.GetVersionHistoryAsync(note.Id, therapist);
        Assert.True(versions.Last().IsSignedVersion);
        Assert.Equal(2, versions.Count); // create + sign
    }

    [Fact]
    public async Task LockNote_ByAdmin_Succeeds_AndThenBlocksNewAddenda()
    {
        var (db, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var admin = Admin(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), therapist);
        await service.SignNoteAsync(note.Id, true, null, therapist);

        // An addendum is fine on a merely-Signed note...
        await service.CreateAddendumAsync(note.Id, new CreateAddendumRequest("typo", "correction"), admin);

        var locked = await service.LockNoteAsync(note.Id, admin);
        Assert.Equal(NoteStatus.Locked, locked.Status);

        // ...but not once it's Locked.
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateAddendumAsync(note.Id, new CreateAddendumRequest("too late", "should fail"), admin));
    }

    [Fact]
    public async Task LockNote_BeforeSigning_Throws()
    {
        var (db, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var admin = Admin(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), therapist);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.LockNoteAsync(note.Id, admin));
    }

    [Fact]
    public async Task LockNote_ByTherapistRole_Throws_OnlyAdminDirectorCanLock()
    {
        var (db, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), therapist);
        await service.SignNoteAsync(note.Id, true, null, therapist);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.LockNoteAsync(note.Id, therapist));
    }

    [Fact]
    public async Task LockedNote_DirectDbWrite_IsStillBlockedByAppendOnlyEnforcement()
    {
        var (db, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var admin = Admin(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), therapist);
        await service.SignNoteAsync(note.Id, true, null, therapist);
        var locked = await service.LockNoteAsync(note.Id, admin);

        locked.Subjective = "Tampering attempt";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task GetVersionHistory_AnotherOrganizationsNote_ThrowsNotFound()
    {
        var (db, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var note = await service.CreateDraftAsync(CompleteDailyNote(patient.Id), therapist);

        var otherOrg = new Organization { Name = "Client B", Slug = "client-b", ClientNumber = 1001 };
        db.Organizations.Add(otherOrg);
        await db.SaveChangesAsync();
        var otherOrgActor = Therapist(otherOrg.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetVersionHistoryAsync(note.Id, otherOrgActor));
    }
}
