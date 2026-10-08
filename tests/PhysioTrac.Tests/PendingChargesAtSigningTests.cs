using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Seed;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Pending (Draft) charges when a note becomes final, and who may
/// add an addendum: signing never fails over billing setup, nothing is
/// submitted, and the organization can turn it off.</summary>
public class PendingChargesAtSigningTests
{
    private sealed record Ctx(PhysioTracDbContext Db, ClinicalNoteService Notes, Organization Org, Patient Pat,
        TestCurrentUser Pt, TestCurrentUser Pta, TestCurrentUser OtherPt);

    private static async Task<Ctx> SetupAsync(bool mapExercise = true)
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional Clinic", Slug = "fictional", Timezone = "America/Chicago" };
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Taylor", LastName = "Sample", DateOfBirth = new DateOnly(1975, 6, 6) };
        db.Organizations.Add(org);
        db.Patients.Add(pat);
        await db.SaveChangesAsync();
        await SystemTemplateSeeder.SeedAsync(db);

        var pt = new TestCurrentUser { UserId = TestTherapists.Add(db, org.Id), OrganizationId = org.Id, Role = UserRole.Therapist };
        var pta = new TestCurrentUser { UserId = TestTherapists.Add(db, org.Id, UserRole.Assistant), OrganizationId = org.Id, Role = UserRole.Assistant };
        var other = new TestCurrentUser { UserId = TestTherapists.Add(db, org.Id), OrganizationId = org.Id, Role = UserRole.Therapist };
        foreach (var u in new[] { pt, pta }) db.Providers.Add(new Provider { OrganizationId = org.Id, UserId = u.UserId, FirstName = "Fictional", LastName = "Clinician" });
        if (mapExercise)
            db.CptCodeMappings.Add(new CptCodeMapping { OrganizationId = org.Id, InterventionCategory = InterventionCategory.TherapeuticExercise, CptCode = "97110", CreatedById = pt.UserId });
        await db.SaveChangesAsync();

        var audit = new AuditService(db);
        var access = new TenantAccessService(db, audit);
        var notes = new ClinicalNoteService(db, access, audit, new AcceptAnySignature(), new ChargeService(db, access));
        return new Ctx(db, notes, org, pat, pt, pta, other);
    }

    /// <summary>A visit note with 23 minutes of therapeutic exercise, ready to sign.</summary>
    private static async Task<Guid> VisitAsync(Ctx c, TestCurrentUser author)
    {
        var at = new DateTimeOffset(2026, 10, 7, 15, 0, 0, TimeSpan.Zero).AddHours(c.Db.Appointments.Count());
        var visit = new Appointment { PatientId = c.Pat.Id, TherapistId = author.UserId, StartsAt = at, EndsAt = at.AddMinutes(45) };
        c.Db.Appointments.Add(visit);
        await c.Db.SaveChangesAsync();
        var noteId = (await c.Notes.OpenAppointmentEncounterAsync(visit.Id, author)).NoteId;
        var v = (await c.Notes.GetEncounterAsync(noteId, author)).SaveVersion;
        await c.Notes.SaveEncounterAsync(noteId, new SaveEncounterRequest(v,
            [new("patientResponse", Text: "Tolerated"), new("continuedSkilledNeed", Text: "Yes")],
            Subjective: "s", Objective: "o", Assessment: "a", Plan: "p",
            Flowsheet: [new FlowsheetEntryDto("Therapeutic exercise", InterventionCategory.TherapeuticExercise, "97110", Minutes: 23)]), author);
        return noteId;
    }

    private static List<Charge> ChargesFor(Ctx c, Guid noteId) => c.Db.Charges.Where(ch => ch.ClinicalNoteId == noteId).ToList();

    [Fact]
    public async Task SigningCreatesDraftChargesForBillingReview()
    {
        var c = await SetupAsync();
        var noteId = await VisitAsync(c, c.Pt);
        Assert.Empty(ChargesFor(c, noteId));

        await c.Notes.SignNoteAsync(noteId, true, null, c.Pt, "pw");

        var charge = Assert.Single(ChargesFor(c, noteId));
        Assert.Equal(("97110", 2, ChargeStatus.Draft), (charge.CptCode, charge.Units, charge.Status)); // 23 min = 2 units, never submitted
        Assert.Single(c.Db.AuditEvents, e => e.Action == "charges.pending_created" && e.ObjectId == noteId);
    }

    [Fact]
    public async Task AnAssistantsNote_IsChargedWhenCosigned_NotWhenSubmitted()
    {
        var c = await SetupAsync();
        var noteId = await VisitAsync(c, c.Pta);
        await c.Notes.SignNoteAsync(noteId, true, null, c.Pta, "pw");
        Assert.Empty(ChargesFor(c, noteId));

        await c.Notes.CosignNoteAsync(noteId, c.Pt, "pw");
        Assert.Single(ChargesFor(c, noteId));
    }

    [Fact]
    public async Task MissingBillingSetup_NeverBlocksSigning()
    {
        var c = await SetupAsync(mapExercise: false);
        var noteId = await VisitAsync(c, c.Pt);
        await c.Notes.SignNoteAsync(noteId, true, null, c.Pt, "pw");

        Assert.Equal(NoteStatus.Signed, (await c.Notes.GetAsync(noteId, c.Pt)).Status);
        Assert.Empty(ChargesFor(c, noteId));
        var skipped = c.Db.AuditEvents.Single(e => e.Action == "charges.pending_skipped" && e.ObjectId == noteId);
        Assert.Contains("No CPT mapping for TherapeuticExercise", skipped.MetadataJson);
    }

    [Fact]
    public async Task TheOrganizationCanTurnItOff()
    {
        var c = await SetupAsync();
        c.Org.AutoCreatePendingCharges = false;
        await c.Db.SaveChangesAsync();
        var noteId = await VisitAsync(c, c.Pt);
        await c.Notes.SignNoteAsync(noteId, true, null, c.Pt, "pw");
        Assert.Empty(ChargesFor(c, noteId));
        Assert.DoesNotContain(c.Db.AuditEvents, e => e.Action.StartsWith("charges."));
    }

    [Fact]
    public async Task AnAmendment_DoesNotChargeTheVisitAgain()
    {
        var c = await SetupAsync();
        var noteId = await VisitAsync(c, c.Pt);
        await c.Notes.SignNoteAsync(noteId, true, null, c.Pt, "pw");
        var amendment = await c.Notes.CreateAmendmentAsync(noteId, new CreateAmendmentRequest("Wrong minutes"), c.Pt);
        await c.Notes.SignNoteAsync(amendment.Id, true, null, c.Pt, "pw");
        Assert.Single(c.Db.Charges);
    }

    [Fact]
    public async Task TheCosigningPT_CanAddAnAddendum_OtherTherapistsCannot()
    {
        var c = await SetupAsync();
        var noteId = await VisitAsync(c, c.Pta);
        await c.Notes.SignNoteAsync(noteId, true, null, c.Pta, "pw");
        await c.Notes.CosignNoteAsync(noteId, c.Pt, "pw");

        await c.Notes.CreateAddendumAsync(noteId, new CreateAddendumRequest("Late entry", "Fictional: called the patient."), c.Pt);
        await c.Notes.CreateAddendumAsync(noteId, new CreateAddendumRequest("Late entry", "Fictional: HEP reviewed."), c.Pta);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            c.Notes.CreateAddendumAsync(noteId, new CreateAddendumRequest("Late entry", "x"), c.OtherPt));
        // Amending stays with the author (or an admin/director).
        Assert.False(c.Notes.CanAmendNote(c.Pt, await c.Notes.GetAsync(noteId, c.Pt)));
    }
}
