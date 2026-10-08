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

/// <summary>Structured objective measurements and the special-test library:
/// rows saved with the encounter and validated, baseline/previous from
/// signed notes, historical values unchanged, library search, favorites,
/// management and activation.</summary>
public class MeasurementsAndSpecialTestsTests
{
    private sealed record Ctx(PhysioTracDbContext Db, ClinicalNoteService Notes, SpecialTestLibraryService Library,
        Organization Org, Patient Pat, TestCurrentUser Therapist, TestCurrentUser Admin);

    private static async Task<Ctx> SetupAsync()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional Clinic", Slug = "fictional" };
        var pat = new Patient { OrganizationId = org.Id, FirstName = "Rowan", LastName = "Sample", DateOfBirth = new DateOnly(1979, 3, 3) };
        db.Organizations.Add(org);
        db.Patients.Add(pat);
        db.SaveChanges();
        await SystemTemplateSeeder.SeedAsync(db);
        await SpecialTestSeeder.SeedAsync(db);
        var audit = new AuditService(db);
        var tenant = new TenantAccessService(db, audit);
        return new Ctx(db, new ClinicalNoteService(db, tenant, audit, new AcceptAnySignature()), new SpecialTestLibraryService(db, tenant, audit),
            org, pat,
            new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist },
            new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin });
    }

    private static Task<ClinicalNote> DailyAsync(Ctx c, DateOnly date) =>
        c.Notes.CreateDraftAsync(new CreateNoteRequest(c.Pat.Id, NoteType.Daily, date, null,
            null, null, null, null, null, null, null, null, null, null), c.Therapist);

    private static ObjectiveMeasurementDto KneeFlexion(decimal deg) =>
        new(MeasurementCategory.RangeOfMotion, "Knee", "Flexion", BodySide.Right, "AROM", deg, Unit: "deg", EndFeel: "Firm", Painful: true);

    private static ObjectiveMeasurementDto QuadMmt(string grade) =>
        new(MeasurementCategory.Strength, "Quadriceps", null, BodySide.Right, "MMT", TextValue: grade, Compensation: "Hip hike");

    private static async Task SignAsync(Ctx c, ClinicalNote note, int baseVersion, IReadOnlyList<ObjectiveMeasurementDto> rows,
        IReadOnlyList<SpecialTestResultDto>? tests = null)
    {
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(baseVersion,
            [new("patientResponse", Text: "Tolerated"), new("continuedSkilledNeed", Text: "Yes")],
            Subjective: "s", Objective: "o", Assessment: "a", Plan: "p", Measurements: rows, SpecialTests: tests), c.Therapist);
        await c.Notes.SignNoteAsync(note.Id, true, null, c.Therapist, "pw");
    }

    [Fact]
    public async Task Measurements_SaveAsStructuredRows_WithUnits()
    {
        var c = await SetupAsync();
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        var gaitSpeed = new ObjectiveMeasurementDto(MeasurementCategory.Gait, "Gait speed", Mode: "10-meter walk", NumericValue: 0.9m, Unit: "m/s",
            AssistiveDevice: "Single-point cane", AssistanceLevel: "Supervision", Surface: "Level indoor");
        var balance = new ObjectiveMeasurementDto(MeasurementCategory.Balance, "Static standing", Condition: "Eyes closed, foam",
            NumericValue: 12, Unit: "sec", TextValue: "Loss of balance");
        await c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(1, Measurements: [KneeFlexion(100), QuadMmt("4-"), gaitSpeed, balance]), c.Therapist);

        var rows = c.Db.ObjectiveMeasurements.Where(m => m.NoteId == note.Id).OrderBy(m => m.Order).ToList();
        Assert.Equal(4, rows.Count);
        Assert.Equal((100m, "deg", "Firm", true), (rows[0].NumericValue!.Value, rows[0].Unit!, rows[0].EndFeel!, rows[0].Painful!.Value));
        Assert.Equal(("4-", "Hip hike"), (rows[1].TextValue!, rows[1].Compensation!));
        Assert.Equal(("m/s", "Single-point cane", "Supervision"), (rows[2].Unit!, rows[2].AssistiveDevice!, rows[2].AssistanceLevel!));
        Assert.All(rows, r => Assert.Equal(c.Pat.Id, r.PatientId));
        var encounter = await c.Notes.GetEncounterAsync(note.Id, c.Therapist);
        Assert.Equal(4, encounter.Measurements!.Count);
    }

    [Theory]
    [InlineData("rom")]
    [InlineData("mmt")]
    [InlineData("item")]
    public async Task InvalidMeasurements_AreRejected(string problem)
    {
        var c = await SetupAsync();
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        ObjectiveMeasurementDto row = problem switch
        {
            "rom" => KneeFlexion(400),
            "mmt" => QuadMmt("6"),
            _ => new ObjectiveMeasurementDto(MeasurementCategory.Reflex, " "),
        };
        await Assert.ThrowsAsync<TemplateValidationException>(() =>
            c.Notes.SaveEncounterAsync(note.Id, new SaveEncounterRequest(1, Measurements: [row]), c.Therapist));
        Assert.Empty(c.Db.ObjectiveMeasurements);
    }

    [Fact]
    public async Task BaselineAndPrevious_ComeFromSignedNotes_AndHistoryIsNeverRewritten()
    {
        var c = await SetupAsync();
        var first = await DailyAsync(c, new DateOnly(2026, 9, 1));
        await SignAsync(c, first, 1, [KneeFlexion(85), QuadMmt("3+")]);
        var second = await DailyAsync(c, new DateOnly(2026, 9, 15));
        await SignAsync(c, second, 1, [KneeFlexion(100), QuadMmt("4-")]);
        var draft = await DailyAsync(c, new DateOnly(2026, 9, 20)); // unsigned: ignored
        await c.Notes.SaveEncounterAsync(draft.Id, new SaveEncounterRequest(1, Measurements: [KneeFlexion(140)]), c.Therapist);

        var today = await DailyAsync(c, new DateOnly(2026, 10, 7));
        var history = (await c.Notes.GetEncounterAsync(today.Id, c.Therapist)).MeasurementHistory!;

        var flexion = history.Single(h => h.Category == MeasurementCategory.RangeOfMotion);
        Assert.Equal((85m, new DateOnly(2026, 9, 1)), (flexion.Baseline.NumericValue!.Value, flexion.Baseline.ServiceDate));
        Assert.Equal((100m, new DateOnly(2026, 9, 15)), (flexion.Previous!.NumericValue!.Value, flexion.Previous.ServiceDate));
        var mmt = history.Single(h => h.Category == MeasurementCategory.Strength);
        Assert.Equal(("3+", "4-"), (mmt.Baseline.TextValue!, mmt.Previous!.TextValue!));

        // The first visit's own view has no previous and itself as baseline; values stay as recorded.
        var firstView = (await c.Notes.GetEncounterAsync(first.Id, c.Therapist)).MeasurementHistory!;
        Assert.Null(firstView.Single(h => h.Category == MeasurementCategory.RangeOfMotion).Previous);
        var stored = c.Db.ObjectiveMeasurements.Single(m => m.NoteId == first.Id && m.Category == MeasurementCategory.RangeOfMotion);
        stored.NumericValue = 90;
        Assert.Throws<InvalidOperationException>(() => c.Db.SaveChanges());
    }

    [Fact]
    public async Task SpecialTests_RecordTheResult_WithTheLastSignedResultForComparison()
    {
        var c = await SetupAsync();
        var lachman = c.Db.SpecialTestDefinitions.Single(d => d.Code == "lachman");
        var hop = c.Db.SpecialTestDefinitions.Single(d => d.Code == "single-hop");
        var first = await DailyAsync(c, new DateOnly(2026, 9, 1));
        await SignAsync(c, first, 1, [], [
            new("Lachman test", SpecialTestOutcome.Positive, lachman.Id, ClinicalSpecialty.Orthopedic, "Knee", BodySide.Right, Interpretation: "Soft end feel"),
            new("Single-leg hop for distance", SpecialTestOutcome.NotTested, hop.Id, ClinicalSpecialty.SportsRehabilitation, Side: BodySide.Right, NumericValue: 92, Unit: "cm"),
        ]);

        var today = await DailyAsync(c, new DateOnly(2026, 10, 7));
        await c.Notes.SaveEncounterAsync(today.Id, new SaveEncounterRequest(1, SpecialTests: [
            new("Lachman test", SpecialTestOutcome.Negative, lachman.Id, ClinicalSpecialty.Orthopedic, "Knee", BodySide.Right),
        ]), c.Therapist);

        var encounter = await c.Notes.GetEncounterAsync(today.Id, c.Therapist);
        Assert.Equal(SpecialTestOutcome.Negative, Assert.Single(encounter.SpecialTests!).Outcome);
        var previous = encounter.SpecialTestHistory!.Single(h => h.TestName == "Lachman test");
        Assert.Equal((SpecialTestOutcome.Positive, new DateOnly(2026, 9, 1)), (previous.Outcome, previous.ServiceDate));
        Assert.Equal(92m, encounter.SpecialTestHistory!.Single(h => h.TestName.StartsWith("Single")).NumericValue);
    }

    [Fact]
    public async Task AResultMustPointAtALibraryTest()
    {
        var c = await SetupAsync();
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        await Assert.ThrowsAsync<TemplateValidationException>(() => c.Notes.SaveEncounterAsync(note.Id,
            new SaveEncounterRequest(1, SpecialTests: [new("Made-up test", SpecialTestOutcome.Positive, Guid.NewGuid())]), c.Therapist));
    }

    [Fact]
    public async Task TheLibrary_IsSearchable_WithFavoritesFirst()
    {
        var c = await SetupAsync();
        var vestibular = await c.Library.SearchAsync(c.Therapist, specialty: ClinicalSpecialty.Vestibular);
        Assert.Contains(vestibular, d => d.Code == "dix-hallpike" && d.ContraindicationWarning != null);
        Assert.All(vestibular, d => Assert.Equal(ClinicalSpecialty.Vestibular, d.Specialty));

        var knee = await c.Library.SearchAsync(c.Therapist, search: "knee");
        Assert.Contains(knee, d => d.Code == "lachman");
        var drawer = knee.Single(d => d.Code == "posterior-drawer-knee");
        await c.Library.SetFavoriteAsync(drawer.Id, true, c.Therapist);
        Assert.Equal(drawer.Id, (await c.Library.SearchAsync(c.Therapist, search: "knee"))[0].Id);
        Assert.Single(await c.Library.SearchAsync(c.Therapist, favoritesOnly: true));
        Assert.True(SpecialTestSeeder.All.Select(d => d.Specialty).Distinct().Count() >= 10);
    }

    [Fact]
    public async Task Administrators_AddEditAndRetireTests_ButBuiltInsOnlyRetire()
    {
        var c = await SetupAsync();
        var mine = await c.Library.CreateAsync(new SaveSpecialTestDefinitionRequest("Clinic hop-and-hold", ClinicalSpecialty.SportsRehabilitation,
            SpecialTestResultKind.Numeric, "Knee", Unit: "sec", ContraindicationWarning: "Not before 12 weeks post ACL reconstruction"), c.Admin);
        Assert.StartsWith("clinic-", mine.Code);
        var edited = await c.Library.UpdateAsync(mine.Id, new SaveSpecialTestDefinitionRequest("Hop and hold", ClinicalSpecialty.SportsRehabilitation,
            SpecialTestResultKind.Numeric, Unit: "sec"), c.Admin);
        Assert.Equal("Hop and hold", edited.Name);

        await Assert.ThrowsAsync<TemplateValidationException>(() => c.Library.CreateAsync(
            new SaveSpecialTestDefinitionRequest("No unit", ClinicalSpecialty.General, SpecialTestResultKind.Numeric), c.Admin));

        var lachman = c.Db.SpecialTestDefinitions.Single(d => d.Code == "lachman");
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Library.UpdateAsync(lachman.Id,
            new SaveSpecialTestDefinitionRequest("Renamed", ClinicalSpecialty.Orthopedic, SpecialTestResultKind.PositiveNegative), c.Admin));
        await c.Library.SetActiveAsync(lachman.Id, false, c.Admin);
        Assert.DoesNotContain(await c.Library.SearchAsync(c.Therapist, search: "Lachman"), d => d.Id == lachman.Id);
        Assert.Contains(await c.Library.SearchAsync(c.Admin, search: "Lachman", includeInactive: true), d => d.Id == lachman.Id && !d.IsActive);

        // Re-seeding refreshes text but never re-activates a retired built-in test.
        await SpecialTestSeeder.SeedAsync(c.Db);
        Assert.False(c.Db.SpecialTestDefinitions.Single(d => d.Code == "lachman").IsActive);
    }

    [Fact]
    public async Task AnAmendment_CarriesMeasurementsAndTests()
    {
        var c = await SetupAsync();
        var note = await DailyAsync(c, new DateOnly(2026, 10, 7));
        var lachman = c.Db.SpecialTestDefinitions.Single(d => d.Code == "lachman");
        await SignAsync(c, note, 1, [KneeFlexion(100)], [new("Lachman test", SpecialTestOutcome.Negative, lachman.Id)]);
        var amendment = await c.Notes.CreateAmendmentAsync(note.Id, new CreateAmendmentRequest("Wrong side"), c.Therapist);
        var copy = await c.Notes.GetEncounterAsync(amendment.Id, c.Therapist);
        Assert.Single(copy.Measurements!);
        Assert.Single(copy.SpecialTests!);
    }
}

/// <summary>Library management needs Admin/Director (access control on).</summary>
[Collection(nameof(AccessControlSwitchCollection))]
public class SpecialTestLibraryAuthorizationTests
{
    [Fact]
    public async Task Clinicians_SearchButCannotChangeTheLibrary()
    {
        AccessControl.Enabled = true;
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional", Slug = "f" };
        db.Organizations.Add(org);
        db.SaveChanges();
        await SpecialTestSeeder.SeedAsync(db);
        var audit = new AuditService(db);
        var library = new SpecialTestLibraryService(db, new TenantAccessService(db, audit), audit);
        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist };
        var biller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Biller };

        Assert.NotEmpty(await library.SearchAsync(therapist));
        await Assert.ThrowsAsync<ForbiddenException>(() => library.CreateAsync(
            new SaveSpecialTestDefinitionRequest("X", ClinicalSpecialty.General, SpecialTestResultKind.PositiveNegative), therapist));
        await Assert.ThrowsAsync<ForbiddenException>(() => library.SetActiveAsync(db.SpecialTestDefinitions.First().Id, false, therapist));
        await Assert.ThrowsAsync<ForbiddenException>(() => library.SearchAsync(biller));
    }
}
