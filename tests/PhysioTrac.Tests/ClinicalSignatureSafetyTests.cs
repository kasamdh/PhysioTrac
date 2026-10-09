using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Accepts any password -- for tests about everything else a
/// signature does. The real check is covered in ClinicalSignatureSafetyTests.</summary>
public sealed class AcceptAnySignature : ISignatureVerifier
{
    public Task VerifyAsync(Guid userId, string? password, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>Documentation Phase 0: password step-up before signing or
/// cosigning, and clinical signing roles that hold even with role checks off.
/// In the non-parallel collection because it switches AccessControl.</summary>
[Collection(nameof(AccessControlSwitchCollection))]
public sealed class ClinicalSignatureSafetyTests : IDisposable
{
    public void Dispose() => AccessControl.Enabled = true;

    private sealed record Ctx(
        PhysioTracDbContext Db, ClinicalNoteService Notes, UserManager<ApplicationUser> Users,
        Organization Org, Patient Pat, ApplicationUser TherapistUser);

    private static async Task<Ctx> SetupAsync(ISignatureVerifier? verifier = null)
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var identity = new IdentityOptions();
        identity.Lockout.MaxFailedAccessAttempts = 3;
        var users = new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser, IdentityRole<Guid>, PhysioTracDbContext, Guid>(db),
            Options.Create(identity), new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(), Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);

        var org = new Organization { Name = "Org", Slug = "org", PtaCosignRequired = true };
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Quinn", LastName = "Alvarez", DateOfBirth = new DateOnly(2001, 7, 30) };
        db.Organizations.Add(org);
        db.Patients.Add(pat);
        await db.SaveChangesAsync();
        var therapistUser = new ApplicationUser { UserName = "therapist", FirstName = "Jamie", LastName = "Chen", OrganizationId = org.Id, Role = UserRole.Therapist };
        await users.CreateAsync(therapistUser, "Correct!Pass1");

        var audit = new AuditService(db);
        var notes = new ClinicalNoteService(db, new TenantAccessService(db, audit), audit, verifier ?? new PasswordSignatureVerifier(users));
        return new Ctx(db, notes, users, org, pat, therapistUser);
    }

    private static TestCurrentUser As(Ctx c, UserRole role, Guid? id = null) =>
        new() { UserId = id ?? Guid.NewGuid(), OrganizationId = c.Org.Id, Role = role };

    private static Task<ClinicalNote> DraftAsync(Ctx c, TestCurrentUser author) =>
        c.Notes.CreateDraftAsync(new CreateNoteRequest(
            c.Pat.Id, NoteType.Daily, new DateOnly(2026, 10, 6), null, "s", "Objective findings", "i", "a", "Plan next visit",
            null, null, null, null, null), author);

    [Fact]
    public async Task Sign_WithTheRightPassword_Signs()
    {
        var c = await SetupAsync();
        var therapist = As(c, UserRole.Therapist, c.TherapistUser.Id);
        var note = await DraftAsync(c, therapist);

        var signed = await c.Notes.SignNoteAsync(note.Id, true, null, therapist, "Correct!Pass1");

        Assert.Equal(NoteStatus.Signed, signed.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-password")]
    public async Task Sign_WithoutTheRightPassword_IsRefused_AndAudited_AndNothingIsSigned(string? password)
    {
        var c = await SetupAsync();
        var therapist = As(c, UserRole.Therapist, c.TherapistUser.Id);
        var note = await DraftAsync(c, therapist);

        await Assert.ThrowsAsync<SignatureVerificationException>(() => c.Notes.SignNoteAsync(note.Id, true, null, therapist, password));

        Assert.Equal(NoteStatus.Draft, (await c.Db.ClinicalNotes.SingleAsync(n => n.Id == note.Id)).Status);
        var failed = await c.Db.AuditEvents.SingleAsync(a => a.Action == "note.signature_reauth_failed");
        Assert.Equal(note.Id, failed.ObjectId);
        Assert.DoesNotContain("wrong-password", failed.MetadataJson);
    }

    [Fact]
    public async Task WrongPasswords_CountTowardLockout_ThenEvenTheRightOneIsRefused()
    {
        var c = await SetupAsync();
        var therapist = As(c, UserRole.Therapist, c.TherapistUser.Id);
        var note = await DraftAsync(c, therapist);

        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<SignatureVerificationException>(() => c.Notes.SignNoteAsync(note.Id, true, null, therapist, "nope"));
        }
        var locked = await Assert.ThrowsAsync<SignatureVerificationException>(() => c.Notes.SignNoteAsync(note.Id, true, null, therapist, "Correct!Pass1"));
        Assert.Contains("locked", locked.Message);
    }

    [Fact]
    public async Task Cosign_NeedsTheCosignersPassword()
    {
        var c = await SetupAsync();
        var assistant = As(c, UserRole.Assistant);
        var note = await DraftAsync(c, assistant);
        await new ClinicalNoteService(c.Db, new TenantAccessService(c.Db, new AuditService(c.Db)), new AuditService(c.Db), new AcceptAnySignature())
            .SignNoteAsync(note.Id, true, null, assistant);
        var supervisor = As(c, UserRole.Therapist, c.TherapistUser.Id);

        await Assert.ThrowsAsync<SignatureVerificationException>(() => c.Notes.CosignNoteAsync(note.Id, supervisor, "wrong"));
        var cosigned = await c.Notes.CosignNoteAsync(note.Id, supervisor, "Correct!Pass1");

        Assert.Equal(NoteStatus.Signed, cosigned.Status);
    }

    [Theory]
    [InlineData(UserRole.Scheduler)]
    [InlineData(UserRole.Biller)]
    public async Task WithRoleChecksOff_NonClinicalStaffStillCannotSign(UserRole role)
    {
        AccessControl.Enabled = false;
        var c = await SetupAsync(new AcceptAnySignature());
        var author = As(c, role);
        var note = await DraftAsync(c, author); // drafting stays open while role checks are off

        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.SignNoteAsync(note.Id, true, null, author, "x"));
    }

    [Fact]
    public async Task WithRoleChecksOff_NonClinicalStaffStillCannotEditOthersNotes_CosignOrLock()
    {
        AccessControl.Enabled = false;
        var c = await SetupAsync(new AcceptAnySignature());
        var assistant = As(c, UserRole.Assistant);
        var biller = As(c, UserRole.Biller);
        var note = await DraftAsync(c, assistant);

        Assert.False(c.Notes.CanEditNote(biller, note));
        await c.Notes.SignNoteAsync(note.Id, true, null, assistant, "x"); // -> awaiting cosign
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.CosignNoteAsync(note.Id, biller, "x"));

        var therapist = As(c, UserRole.Therapist);
        var own = await DraftAsync(c, therapist);
        await c.Notes.SignNoteAsync(own.Id, true, null, therapist, "x");
        await Assert.ThrowsAsync<ForbiddenException>(() => c.Notes.LockNoteAsync(own.Id, biller));
    }
}
