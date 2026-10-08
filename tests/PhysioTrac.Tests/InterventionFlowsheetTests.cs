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

/// <summary>Intervention / exercise flowsheet: structured entries saved
/// with the encounter, advisory billing checks, selective carry-forward
/// (audited, must be reviewed before signing), the library and groups.</summary>
public class InterventionFlowsheetTests
{
    private sealed record Ctx(PhysioTracDbContext Db, ClinicalNoteService Notes, InterventionLibraryService Library,
        Organization Org, Patient Pat, TestCurrentUser Therapist, TestCurrentUser Admin);

    private static async Task<Ctx> SetupAsync()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional Clinic", Slug = "fictional" };
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Taylor", LastName = "Sample", DateOfBirth = new DateOnly(1975, 6, 6) };
        db.Organizations.Add(org);
        db.Patients.Add(pat);
        db.SaveChanges();
        await SystemTemplateSeeder.SeedAsync(db);
        await InterventionLibrarySeeder.SeedAsync(db);
        var audit = new AuditService(db);
        var tenant = new TenantAccessService(db, audit);
        return new Ctx(db, new ClinicalNoteService(db, tenant, audit, new AcceptAnySignature()), new InterventionLibraryService(db, tenant, audit),
            org, pat,
            new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist },
            new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin });
    }

    private static Task<ClinicalNote> DailyAsync(Ctx c, DateOnly date) =>
        c.Notes.CreateDraftAsync(new CreateNoteRequest(c.Pat.Id, NoteType.Daily, date, null,
            null, null, null, null, null, null, null, null, null, null), c.Therapist);

    private static FlowsheetEntryDto Bridges(int minutes = 12, string? response = "Tolerated well") =>
        new("Bridges", InterventionCategory.TherapeuticExercise, "97110", true, new TimeOnly(9, 0), new TimeOnly(9, minutes), minutes,
            Sets: 3, Repetitions: 10, Resistance: "Body weight", Position: "Supine", PatientResponse: response, PainBefore: 4, PainAfter: 2);

    private static FlowsheetEntryDto Mobs(TimeOnly start, int minutes) =>
        new("Joint mobilization", InterventionCategory.ManualTherapy, "97140", true, start, start.AddMinutes(minutes), minutes,
            PatientResponse: "Less stiffness");

    private static async Task SignAsync(Ctx c, ClinicalNote note, IReadOnlyList<FlowsheetEntryDto> flowsheet)
    {
        var v = (await c.Notes.GetEncounterAsync(note.Id, c.Therapist)).SaveVersion;
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(v,
            [new("patientResponse", Text: "Tolerated"), new("continuedSkilledNeed", Text: "Yes")],
            Subjective: "s", Objective: "o", Assessment: "a", Plan: "p", Flowsheet: flowsheet), c.Therapist);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw");
    }

    [Fact]
    public async Task TheFlowsheet_SavesEveryField_AndTotalsTheTime()
    {
        var c = await SetupAsync();
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        var hot = new FlowsheetEntryDto("Hot pack", InterventionCategory.Modalities, "97010", false, Minutes: 10, PatientResponse: "Relaxed");
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(1, Flowsheet: [Bridges(), Mobs(new TimeOnly(9, 15), 15), hot]), c.Therapist);

        var row = c.Db.NoteInterventions.Single(i => i.NoteId == note.Id && i.Description == "Bridges");
        Assert.Equal(("97110", 3, 10, "Body weight", 4, 2), (row.CptCode!, row.Sets!.Value, row.Repetitions!.Value, row.Resistance!, row.PainBefore!.Value, row.PainAfter!.Value));
        Assert.Equal((new TimeOnly(9, 0), InterventionStatus.Completed), (row.StartTime!.Value, row.Status));

        var encounter = await c.Notes.GetEncounterAsync(note.Id, c.Therapist);
        Assert.Equal(3, encounter.Flowsheet!.Count);
        var summary = encounter.FlowsheetSummary!;
        Assert.Equal((27, 1, 2), (summary.TimedMinutes, summary.UntimedServices, summary.EstimatedTimedUnits));
        Assert.Empty(summary.Warnings);
    }

    [Fact]
    public void TheChecks_FlagOverlapMissingResponseMinutesAndUnits()
    {
        var entries = new List<FlowsheetEntryDto>
        {
            Bridges(12, response: null),                                          // no response
            Mobs(new TimeOnly(9, 5), 15),                                         // overlaps 9:00-9:12
            Bridges(8) with { Description = "Clamshells", StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 20), Units = 3 },
            new("Ultrasound", InterventionCategory.Modalities, "97035", true, Minutes: 0, PatientResponse: "Fine"),
            new("Hot pack", InterventionCategory.Modalities, "97010", false, Minutes: 10, Units: 2, PatientResponse: "Fine"),
            new("Held: e-stim", InterventionCategory.Modalities, "97032", true, Minutes: 15, Status: InterventionStatus.Held),
        };
        var summary = FlowsheetRules.Analyze(entries, EightMinuteRuleVariant.Medicare);
        var codes = summary.Warnings.Select(w => w.Code).ToHashSet();

        Assert.Superset(new HashSet<string> { "missing_response", "overlap", "minutes_mismatch", "no_minutes", "untimed_units", "units_mismatch" }, codes);
        Assert.Equal(6, codes.Count);
        Assert.Equal(35, summary.TimedMinutes); // held entry excluded
    }

    [Fact]
    public async Task InvalidEntries_AreRejected()
    {
        var c = await SetupAsync();
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        await Assert.ThrowsAsync<TemplateValidationException>(() => c.Notes.SaveEncounterAsync(note.Id,
            new SaveEncounterRequest(1, Flowsheet: [Bridges() with { PainAfter = 12 }]), c.Therapist));
        await Assert.ThrowsAsync<TemplateValidationException>(() => c.Notes.SaveEncounterAsync(note.Id,
            new SaveEncounterRequest(1, Flowsheet: [Bridges() with { LibraryItemId = Guid.NewGuid() }]), c.Therapist));
        Assert.Empty(c.Db.NoteInterventions);
    }

    [Fact]
    public async Task CarryForward_IsSelective_Audited_AndMustBeReviewedBeforeSigning()
    {
        var c = await SetupAsync();
        var last = await DailyAsync(c, new DateOnly(2026, 10, 1));
        await SignAsync(c, last, [Bridges(), Mobs(new TimeOnly(9, 15), 15)]);

        var today = await DailyAsync(c, new DateOnly(2026, 10, 7));
        var encounter = await c.Notes.GetEncounterAsync(today.Id, c.Therapist);
        Assert.Equal((last.Id, 2), (encounter.PreviousFlowsheet!.NoteId, encounter.PreviousFlowsheet.Entries.Count));
        Assert.Empty(encounter.Flowsheet!); // nothing is copied automatically

        // The therapist picks one entry to bring forward.
        var picked = encounter.PreviousFlowsheet.Entries[0] with { Id = null, CarriedForwardFromNoteId = last.Id, CarryForwardReviewed = false };
        var v = (await c.Notes.SaveEncounterAsync(today.Id, new SaveEncounterRequest(encounter.SaveVersion,
            [new("patientResponse", Text: "Tolerated"), new("continuedSkilledNeed", Text: "Yes")],
            Subjective: "s", Objective: "o", Assessment: "a", Plan: "p", Flowsheet: [picked]), c.Therapist)).SaveVersion;

        var audit = Assert.Single(c.Db.AuditEvents.Where(e => e.Action == "note.carry_forward"));
        Assert.Contains(last.Id.ToString(), audit.MetadataJson);
        Assert.Contains(await c.Notes.GetComplianceAsync(today.Id, c.Therapist), f => f.Code == "carry_forward_unreviewed" && f.FinalizationBlocker);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Notes.SignNoteAsync(today.Id, true, null, c.Therapist, "pw"));

        // Re-saving the same carried entry does not log another carry-forward; reviewing it unblocks signing.
        await c.Notes.SaveEncounterAsync(today.Id, new SaveEncounterRequest(v, Flowsheet: [picked with { CarryForwardReviewed = true }]), c.Therapist);
        Assert.Single(c.Db.AuditEvents.Where(e => e.Action == "note.carry_forward"));
        await c.Notes.SignNoteAsync(today.Id, true, null, c.Therapist, "pw");
    }

    [Fact]
    public async Task CarryForward_OnlyFromThisPatientsNotes()
    {
        var c = await SetupAsync();
        var other = new Patient { OrganizationId = c.Org.Id, FirstName = "Other", LastName = "Person", DateOfBirth = new DateOnly(1990, 1, 1) };
        var otherNote = new ClinicalNote { PatientId = other.Id, TherapistId = c.Therapist.UserId };
        c.Db.AddRange(other, otherNote);
        await c.Db.SaveChangesAsync();
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        await Assert.ThrowsAsync<TemplateValidationException>(() => c.Notes.SaveEncounterAsync(note.Id,
            new SaveEncounterRequest(1, Flowsheet: [Bridges() with { CarriedForwardFromNoteId = otherNote.Id }]), c.Therapist));
    }

    [Fact]
    public async Task ASignedFlowsheet_IsFrozen_AndCoveredByTheSignature()
    {
        var c = await SetupAsync();
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        await SignAsync(c, note, [Bridges()]);
        var row = c.Db.NoteInterventions.Single();
        row.Sets = 5;
        Assert.Throws<InvalidOperationException>(() => c.Db.SaveChanges());
        Assert.Contains("Flowsheet", c.Db.ClinicalNoteVersions.Where(v => v.NoteId == note.Id && v.IsSignedVersion).Single().ContentJson);
    }

    [Fact]
    public async Task TheLibrary_SearchesByNameOrCode_WithFavoritesFirst()
    {
        var c = await SetupAsync();
        var byCode = await c.Library.SearchAsync(c.Therapist, "97140");
        Assert.All(byCode, i => Assert.Equal("97140", i.CptCode));
        var epley = (await c.Library.SearchAsync(c.Therapist, "epley")).Single();
        Assert.Equal((InterventionCategory.CanalithRepositioning, false), (epley.Category, epley.IsTimed));
        await c.Library.SetItemFavoriteAsync(epley.Id, true, c.Therapist);
        Assert.Equal(epley.Id, (await c.Library.SearchAsync(c.Therapist))[0].Id);
        Assert.True(InterventionLibrarySeeder.All.Select(i => i.Category).Distinct().Count() >= 11);
    }

    [Fact]
    public async Task Groups_ArePersonalOrShared_AndOnlyTheOwnerChangesThem()
    {
        var c = await SetupAsync();
        var mine = await c.Library.CreateGroupAsync(new SaveInterventionGroupRequest("Knee strength",
            [new("Quad sets", InterventionCategory.TherapeuticExercise, Sets: 3, Repetitions: 10), new("Step-ups", InterventionCategory.TherapeuticActivity)]), c.Therapist);
        var shared = await c.Library.CreateGroupAsync(new SaveInterventionGroupRequest("Clinic vestibular set",
            [new("Epley maneuver", InterventionCategory.CanalithRepositioning, false)], IsShared: true), c.Admin);

        var colleague = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = c.Org.Id, Role = UserRole.Therapist };
        var seen = await c.Library.ListGroupsAsync(colleague);
        Assert.Equal(new[] { "Clinic vestibular set" }, seen.Select(g => g.Name).ToArray());
        await Assert.ThrowsAsync<NotFoundException>(() => c.Library.UpdateGroupAsync(mine.Id,
            new SaveInterventionGroupRequest("Hijack", [new("X", InterventionCategory.Other)]), colleague));

        var renamed = await c.Library.UpdateGroupAsync(mine.Id, new SaveInterventionGroupRequest("Knee strength v2",
            [new("Quad sets", InterventionCategory.TherapeuticExercise)]), c.Therapist);
        Assert.Equal(("Knee strength v2", 1), (renamed.Name, renamed.Items.Count));
        await c.Library.DeleteGroupAsync(mine.Id, c.Therapist);
        Assert.DoesNotContain(await c.Library.ListGroupsAsync(c.Therapist), g => g.Id == mine.Id);
        Assert.True(shared.IsShared);
    }

    [Fact]
    public async Task AnAmendment_CarriesTheFullFlowsheet()
    {
        var c = await SetupAsync();
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        await SignAsync(c, note, [Bridges()]);
        var amendment = await c.Notes.CreateAmendmentAsync(note.Id, new CreateAmendmentRequest("Wrong reps"), c.Therapist);
        var copy = Assert.Single((await c.Notes.GetEncounterAsync(amendment.Id, c.Therapist)).Flowsheet!);
        Assert.Equal((3, 10, "97110", 4), (copy.Sets!.Value, copy.Repetitions!.Value, copy.CptCode!, copy.PainBefore!.Value));
    }
}

/// <summary>Library and shared-group management need Admin/Director (access control on).</summary>
[Collection(nameof(AccessControlSwitchCollection))]
public class InterventionLibraryAuthorizationTests
{
    [Fact]
    public async Task Clinicians_UseButCannotManageTheLibraryOrSharedGroups()
    {
        AccessControl.Enabled = true;
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional", Slug = "f" };
        db.Organizations.Add(org);
        db.SaveChanges();
        await InterventionLibrarySeeder.SeedAsync(db);
        var audit = new AuditService(db);
        var library = new InterventionLibraryService(db, new TenantAccessService(db, audit), audit);
        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist };

        Assert.NotEmpty(await library.SearchAsync(therapist));
        await Assert.ThrowsAsync<ForbiddenException>(() => library.CreateItemAsync(
            new SaveInterventionLibraryItemRequest("X", InterventionCategory.Other, true), therapist));
        await Assert.ThrowsAsync<ForbiddenException>(() => library.CreateGroupAsync(
            new SaveInterventionGroupRequest("Shared", [new("A", InterventionCategory.Other)], IsShared: true), therapist));
        Assert.NotNull(await library.CreateGroupAsync(new SaveInterventionGroupRequest("Mine", [new("A", InterventionCategory.Other)]), therapist));
    }
}
