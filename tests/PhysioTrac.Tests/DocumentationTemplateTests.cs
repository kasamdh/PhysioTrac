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

/// <summary>Configurable documentation templates: creation, versioning
/// (an edit publishes a new version; notes keep theirs), system templates,
/// validation, authorization, favorites and suggestion.</summary>
public class DocumentationTemplateTests
{
    private sealed record Ctx(PhysioTracDbContext Db, DocumentationTemplateService Templates, Organization Org, TestCurrentUser Admin, TestCurrentUser Therapist);

    private static Ctx Setup()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional Clinic", Slug = "fictional" };
        db.Organizations.Add(org);
        db.SaveChanges();
        var audit = new AuditService(db);
        var templates = new DocumentationTemplateService(db, new TenantAccessService(db, audit), audit);
        return new Ctx(db, templates, org,
            new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin },
            new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist });
    }

    private static SaveDocumentationTemplateRequest Request(string name = "Knee daily", params TemplateFieldDto[] extra) => new(
        name, NoteType.Daily, ClinicalSpecialty.Orthopedic,
        [
            new TemplateSectionDto("subjective", "Subjective",
            [
                new TemplateFieldDto("report", "Patient report", TemplateFieldType.LongText, IsRequired: true, NoteColumn: "subjective"),
                new TemplateFieldDto("pain", "Pain now", TemplateFieldType.PainScale, ScaleMin: 0, ScaleMax: 10),
                .. extra,
            ]),
        ]);

    [Fact]
    public async Task Create_PublishesVersionOne()
    {
        var c = Setup();
        var created = await c.Templates.CreateAsync(Request(), c.Admin);

        Assert.Equal((1, false, true), (created.Template.CurrentVersionNumber, created.Template.IsSystem, created.Template.IsActive));
        var section = Assert.Single(created.CurrentVersion.Sections);
        Assert.Equal(new[] { "report", "pain" }, section.Fields.Select(f => f.Key).ToArray());
        Assert.Equal((0m, 10m), (section.Fields[1].ScaleMin!.Value, section.Fields[1].ScaleMax!.Value));
        Assert.Contains(c.Db.AuditEvents, e => e.Action == "template.created");
    }

    [Fact]
    public async Task EditingTheFields_PublishesANewVersion_AndLeavesTheOldOneUnchanged()
    {
        var c = Setup();
        var v1 = await c.Templates.CreateAsync(Request(), c.Admin);
        var v2 = await c.Templates.UpdateAsync(v1.Template.Id,
            Request(extra: new TemplateFieldDto("swelling", "Swelling", TemplateFieldType.Radio, Options: ["None", "Mild", "Moderate"])) with { ChangeSummary = "Added swelling" },
            c.Admin);

        Assert.Equal(2, v2.Template.CurrentVersionNumber);
        Assert.NotEqual(v1.CurrentVersion.Id, v2.CurrentVersion.Id);
        var old = await c.Templates.GetVersionAsync(v1.CurrentVersion.Id, c.Therapist);
        Assert.Equal(2, old.Sections[0].Fields.Count); // version 1 still has two fields
        Assert.Equal(3, v2.CurrentVersion.Sections[0].Fields.Count);
        var history = await c.Templates.ListVersionsAsync(v1.Template.Id, c.Admin);
        Assert.Equal(new[] { 2, 1 }, history.Select(h => h.VersionNumber).ToArray());
        Assert.Equal("Added swelling", history[0].ChangeSummary);
    }

    [Fact]
    public async Task SavingWithoutFieldChanges_UpdatesDetailsWithoutANewVersion()
    {
        var c = Setup();
        var v1 = await c.Templates.CreateAsync(Request(), c.Admin);
        var saved = await c.Templates.UpdateAsync(v1.Template.Id, Request("Knee daily — renamed"), c.Admin);
        Assert.Equal((1, "Knee daily — renamed"), (saved.Template.CurrentVersionNumber, saved.Template.Name));
    }

    [Fact]
    public async Task ANote_KeepsTheTemplateVersionItWasWrittenWith()
    {
        var c = Setup();
        var v1 = await c.Templates.CreateAsync(Request(), c.Admin);
        var note = new ClinicalNote { PatientId = Guid.NewGuid(), TherapistId = c.Therapist.UserId, TemplateVersionId = v1.CurrentVersion.Id };
        c.Db.ClinicalNotes.Add(note);
        await c.Db.SaveChangesAsync();

        await c.Templates.UpdateAsync(v1.Template.Id, Request(extra: new TemplateFieldDto("notes", "Notes", TemplateFieldType.ShortText)), c.Admin);

        Assert.Equal(v1.CurrentVersion.Id, (await c.Db.ClinicalNotes.FindAsync(note.Id))!.TemplateVersionId);
        var versions = await c.Templates.ListVersionsAsync(v1.Template.Id, c.Admin);
        Assert.Equal(1, versions.Single(v => v.VersionNumber == 1).NotesUsing);
    }

    [Fact]
    public async Task SystemTemplates_AreSeededReadOnly_AndCanBeCopied()
    {
        var c = Setup();
        await SystemTemplateSeeder.SeedAsync(c.Db);
        var list = await c.Templates.ListAsync(c.Therapist);
        Assert.Equal(SystemTemplates.All.Count, list.Count(t => t.IsSystem));
        var daily = list.Single(t => t.Name == "Daily Treatment SOAP Note");

        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Templates.UpdateAsync(daily.Id, Request(), c.Admin));
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Templates.SetActiveAsync(daily.Id, false, c.Admin));

        var copy = await c.Templates.CopyAsync(daily.Id, new CopyTemplateRequest("Our daily note"), c.Admin);
        Assert.False(copy.Template.IsSystem);
        Assert.Equal(1, copy.Template.CurrentVersionNumber);
        var source = await c.Templates.GetAsync(daily.Id, c.Admin);
        Assert.Equal(source.CurrentVersion.Sections.Sum(s => s.Fields.Count), copy.CurrentVersion.Sections.Sum(s => s.Fields.Count));
    }

    [Fact]
    public void EverySystemTemplate_IsAValidDefinition()
    {
        foreach (var t in SystemTemplates.All)
        {
            Assert.Empty(TemplateRules.ValidateDefinition(t.Name, t.Sections));
        }
        Assert.Equal(10, SystemTemplates.All.Select(t => t.NoteType).Distinct().Count());
    }

    [Fact]
    public async Task ReSeeding_IsANoOp_ButAChangedDefinitionPublishesANewSystemVersion()
    {
        var c = Setup();
        await SystemTemplateSeeder.SeedAsync(c.Db);
        await SystemTemplateSeeder.SeedAsync(c.Db);
        Assert.Equal(SystemTemplates.All.Count, c.Db.ClinicalNoteTemplateVersions.Count());

        var changed = SystemTemplates.Consultation() with
        {
            Sections = [.. SystemTemplates.Consultation().Sections, new TemplateSectionDto("extra", "Extra", [new TemplateFieldDto("x", "X", TemplateFieldType.ShortText)])],
        };
        await SystemTemplateSeeder.SeedAsync(c.Db, [changed]);
        var consult = c.Db.ClinicalNoteTemplates.Single(t => t.TemplateKey == "consultation");
        Assert.Equal(new[] { 1, 2 }, c.Db.ClinicalNoteTemplateVersions.Where(v => v.TemplateId == consult.Id).Select(v => v.VersionNumber).OrderBy(n => n).ToArray());
    }

    [Theory]
    [InlineData("dup")]
    [InlineData("noOptions")]
    [InlineData("badCondition")]
    [InlineData("badPattern")]
    [InlineData("minOverMax")]
    [InlineData("columnTwice")]
    public async Task InvalidDefinitions_AreRejected_WithTheReasons(string problem)
    {
        var c = Setup();
        TemplateFieldDto field = problem switch
        {
            "dup" => new("pain", "Pain again", TemplateFieldType.Number),
            "noOptions" => new("side", "Side", TemplateFieldType.Select),
            "badCondition" => new("detail", "Detail", TemplateFieldType.ShortText, Condition: new FieldConditionDto("missing", "Yes")),
            "badPattern" => new("code", "Code", TemplateFieldType.ShortText, Validation: new FieldValidationDto(Pattern: "([")),
            "minOverMax" => new("reps", "Reps", TemplateFieldType.Number, Validation: new FieldValidationDto(10, 1)),
            _ => new("summary", "Summary", TemplateFieldType.LongText, NoteColumn: "subjective"),
        };
        var ex = await Assert.ThrowsAsync<TemplateValidationException>(() => c.Templates.CreateAsync(Request(extra: field), c.Admin));
        Assert.NotEmpty(ex.Errors);
        Assert.Empty(c.Db.ClinicalNoteTemplates);
    }

    [Fact]
    public async Task TemplateFavorites_ComeFirst_AndDriveTheSuggestion()
    {
        var c = Setup();
        await SystemTemplateSeeder.SeedAsync(c.Db);
        var mine = await c.Templates.CreateAsync(Request("Knee daily"), c.Admin);
        var other = await c.Templates.CreateAsync(Request("Shoulder daily"), c.Admin);

        await c.Templates.SetFavoriteAsync(other.Template.Id, true, c.Therapist);
        var list = await c.Templates.ListAsync(c.Therapist, NoteType.Daily);
        Assert.Equal("Shoulder daily", list[0].Name);
        Assert.True(list[0].IsFavorite);
        Assert.Equal(other.Template.Id, (await c.Templates.SuggestAsync(c.Therapist, NoteType.Daily))!.Id);

        await c.Templates.SetFavoriteAsync(other.Template.Id, false, c.Therapist);
        Assert.DoesNotContain(await c.Templates.ListAsync(c.Therapist, NoteType.Daily), t => t.IsFavorite);
        Assert.NotNull(mine);
    }

    [Fact]
    public async Task ATemplateAssignedToTheAppointmentType_IsSuggestedFirst()
    {
        var c = Setup();
        await SystemTemplateSeeder.SeedAsync(c.Db);
        var type = new AppointmentType { OrganizationId = c.Org.Id, Name = "Knee follow-up" };
        c.Db.AppointmentTypes.Add(type);
        await c.Db.SaveChangesAsync();
        var assigned = await c.Templates.CreateAsync(Request() with { AppointmentTypeIds = [type.Id] }, c.Admin);

        Assert.Equal(assigned.Template.Id, (await c.Templates.SuggestAsync(c.Therapist, NoteType.Daily, type.Id))!.Id);
        // A progress note has no clinic template, so the system one is suggested.
        var progress = await c.Templates.SuggestAsync(c.Therapist, NoteType.Progress);
        Assert.True(progress!.IsSystem);
    }

    [Fact]
    public async Task InactiveTemplates_AreHiddenUnlessAsked()
    {
        var c = Setup();
        var t = await c.Templates.CreateAsync(Request(), c.Admin);
        await c.Templates.SetActiveAsync(t.Template.Id, false, c.Admin);

        Assert.Empty(await c.Templates.ListAsync(c.Therapist));
        Assert.Single(await c.Templates.ListAsync(c.Admin, includeInactive: true));
        Assert.Null(await c.Templates.SuggestAsync(c.Therapist, NoteType.Daily));
    }

    [Fact]
    public async Task AnotherClinicsTemplates_AreInvisible()
    {
        var c = Setup();
        var t = await c.Templates.CreateAsync(Request(), c.Admin);
        var otherOrg = new Organization { Name = "Other", Slug = "other" };
        c.Db.Organizations.Add(otherOrg);
        await c.Db.SaveChangesAsync();
        var outsider = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = otherOrg.Id, Role = UserRole.Admin };

        await Assert.ThrowsAsync<NotFoundException>(() => c.Templates.GetAsync(t.Template.Id, outsider));
        await Assert.ThrowsAsync<NotFoundException>(() => c.Templates.GetVersionAsync(t.CurrentVersion.Id, outsider));
        Assert.Empty(await c.Templates.ListAsync(outsider));
    }
}

/// <summary>Role checks for template management (role-based access control on).</summary>
[Collection(nameof(AccessControlSwitchCollection))]
public class DocumentationTemplateAuthorizationTests
{
    [Theory]
    [InlineData(UserRole.Therapist)]
    [InlineData(UserRole.Assistant)]
    [InlineData(UserRole.Scheduler)]
    public async Task OnlyAdministratorsAndDirectors_ManageTemplates(UserRole role)
    {
        AccessControl.Enabled = true;
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var org = new Organization { Name = "Fictional", Slug = "f" };
        db.Organizations.Add(org);
        db.SaveChanges();
        var audit = new AuditService(db);
        var service = new DocumentationTemplateService(db, new TenantAccessService(db, audit), audit);
        var user = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = role };
        var director = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Director };
        var request = new SaveDocumentationTemplateRequest("T", NoteType.Daily, ClinicalSpecialty.General,
            [new TemplateSectionDto("s", "S", [new TemplateFieldDto("a", "A", TemplateFieldType.ShortText)])]);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(request, user));
        var made = await service.CreateAsync(request, director);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateAsync(made.Template.Id, request, user));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.SetActiveAsync(made.Template.Id, false, user));

        if (role == UserRole.Scheduler)
            await Assert.ThrowsAsync<ForbiddenException>(() => service.ListAsync(user)); // not clinical staff
        else
            Assert.Single(await service.ListAsync(user)); // clinicians can read and use them
    }
}
