using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>The template engine's two real jobs: versioning (a new template
/// for the same scope supersedes, never overwrites, the old one) and
/// resolution (most-specific-wins: Location > State > Organization >
/// Platform default).</summary>
public class ClinicalTemplateServiceTests
{
    private static (PhysioTracDbContext Db, ClinicalTemplateService Service, Organization Org, Location Location)
        NewService()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var location = new Location { OrganizationId = org.Id, Name = "Main Clinic", State = "NC" };
        db.Organizations.Add(org);
        db.Locations.Add(location);
        db.SaveChanges();

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var service = new ClinicalTemplateService(db, tenantAccess, audit);
        return (db, service, org, location);
    }

    private static TestCurrentUser Admin(Guid orgId) => new() { UserId = Guid.NewGuid(), OrganizationId = orgId, Role = UserRole.Admin };
    private static TestCurrentUser SuperAdmin() => new() { UserId = Guid.NewGuid(), OrganizationId = null, Role = UserRole.SuperAdmin, IsPlatformSuperAdmin = true };

    private const string SampleSchema = """{"sections":[{"key":"objective","fields":[{"key":"pain","type":"painScale0to10"}]}]}""";

    [Fact]
    public async Task Create_OrganizationScope_StartsAtVersion1()
    {
        var (db, service, org, _) = NewService();
        var template = await service.CreateAsync(
            new CreateClinicalNoteTemplateRequest(NoteType.Daily, TemplateScope.Organization, null, null, "Org Daily Template", SampleSchema), Admin(org.Id));

        Assert.Equal(1, template.Version);
        Assert.True(template.IsActive);
    }

    [Fact]
    public async Task Create_SecondVersionForTheSameScope_DeactivatesThePrevious_AndIncrementsVersion()
    {
        var (db, service, org, _) = NewService();
        var admin = Admin(org.Id);
        var v1 = await service.CreateAsync(
            new CreateClinicalNoteTemplateRequest(NoteType.Daily, TemplateScope.Organization, null, null, "v1", SampleSchema), admin);
        var v2 = await service.CreateAsync(
            new CreateClinicalNoteTemplateRequest(NoteType.Daily, TemplateScope.Organization, null, null, "v2", SampleSchema), admin);

        Assert.Equal(2, v2.Version);
        var reloadedV1 = await db.ClinicalNoteTemplates.FindAsync(v1.Id);
        Assert.False(reloadedV1!.IsActive);
        // v1's row itself is never deleted -- full history stays queryable.
        Assert.Equal(2, await db.ClinicalNoteTemplates.CountAsync());
    }

    [Fact]
    public async Task Create_InvalidJson_Throws()
    {
        var (_, service, org, _) = NewService();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            new CreateClinicalNoteTemplateRequest(NoteType.Daily, TemplateScope.Organization, null, null, "Bad", "{not json"), Admin(org.Id)));
    }

    [Fact]
    public async Task Create_StateScope_RequiresATwoLetterCode()
    {
        var (_, service, org, _) = NewService();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            new CreateClinicalNoteTemplateRequest(NoteType.Daily, TemplateScope.State, "North Carolina", null, "Bad state", SampleSchema), Admin(org.Id)));
    }

    [Fact]
    public async Task Create_LocationScope_RejectsALocationFromAnotherOrganization()
    {
        var (db, service, org, _) = NewService();
        var otherOrg = new Organization { Name = "Client B", Slug = "client-b" };
        var otherLocation = new Location { OrganizationId = otherOrg.Id, Name = "Other Clinic" };
        db.Organizations.Add(otherOrg);
        db.Locations.Add(otherLocation);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(
            new CreateClinicalNoteTemplateRequest(NoteType.Daily, TemplateScope.Location, null, otherLocation.Id, "Bad location", SampleSchema), Admin(org.Id)));
    }

    [Fact]
    public async Task Create_PlatformScope_RequiresPlatformSuperAdmin_NotJustOrgAdmin()
    {
        var (_, service, org, _) = NewService();
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(
            new CreateClinicalNoteTemplateRequest(NoteType.Daily, TemplateScope.Platform, null, null, "Global default", SampleSchema), Admin(org.Id)));
    }

    [Fact]
    public async Task Resolve_PrefersLocation_ThenState_ThenOrganization_ThenPlatform()
    {
        var (db, service, org, location) = NewService();
        var admin = Admin(org.Id);

        var platformTemplate = await service.CreateAsync(
            new CreateClinicalNoteTemplateRequest(NoteType.Daily, TemplateScope.Platform, null, null, "Platform default", SampleSchema), SuperAdmin());

        // With only the Platform default configured, resolution falls all the way back to it.
        var resolved1 = await service.ResolveAsync(admin, NoteType.Daily, location.Id, "NC");
        Assert.Equal(platformTemplate.Id, resolved1!.Id);

        var orgTemplate = await service.CreateAsync(
            new CreateClinicalNoteTemplateRequest(NoteType.Daily, TemplateScope.Organization, null, null, "Org default", SampleSchema), admin);
        var resolved2 = await service.ResolveAsync(admin, NoteType.Daily, location.Id, "NC");
        Assert.Equal(orgTemplate.Id, resolved2!.Id);

        var stateTemplate = await service.CreateAsync(
            new CreateClinicalNoteTemplateRequest(NoteType.Daily, TemplateScope.State, "NC", null, "NC template", SampleSchema), admin);
        var resolved3 = await service.ResolveAsync(admin, NoteType.Daily, location.Id, "NC");
        Assert.Equal(stateTemplate.Id, resolved3!.Id);

        var locationTemplate = await service.CreateAsync(
            new CreateClinicalNoteTemplateRequest(NoteType.Daily, TemplateScope.Location, null, location.Id, "Location template", SampleSchema), admin);
        var resolved4 = await service.ResolveAsync(admin, NoteType.Daily, location.Id, "NC");
        Assert.Equal(locationTemplate.Id, resolved4!.Id);
    }

    [Fact]
    public async Task Resolve_ReturnsNull_WhenNothingIsConfiguredAtAnyLevel()
    {
        var (_, service, org, location) = NewService();
        var resolved = await service.ResolveAsync(Admin(org.Id), NoteType.Evaluation, location.Id, "NC");
        Assert.Null(resolved);
    }

    [Fact]
    public async Task Resolve_NeverReturnsAnotherOrganizationsOrganizationScopeTemplate()
    {
        var (db, service, org, _) = NewService();
        var otherOrg = new Organization { Name = "Client B", Slug = "client-b" };
        db.Organizations.Add(otherOrg);
        await db.SaveChangesAsync();
        await service.CreateAsync(
            new CreateClinicalNoteTemplateRequest(NoteType.Daily, TemplateScope.Organization, null, null, "Other org's template", SampleSchema),
            Admin(otherOrg.Id));

        var resolved = await service.ResolveAsync(Admin(org.Id), NoteType.Daily, null, null);
        Assert.Null(resolved);
    }

    [Fact]
    public async Task List_IncludesOwnOrganizationTemplatesAndPlatformDefaults_ButNotAnotherOrganizationsTemplates()
    {
        var (db, service, org, _) = NewService();
        var otherOrg = new Organization { Name = "Client B", Slug = "client-b" };
        db.Organizations.Add(otherOrg);
        await db.SaveChangesAsync();

        await service.CreateAsync(new CreateClinicalNoteTemplateRequest(NoteType.Daily, TemplateScope.Organization, null, null, "Mine", SampleSchema), Admin(org.Id));
        await service.CreateAsync(new CreateClinicalNoteTemplateRequest(NoteType.Daily, TemplateScope.Organization, null, null, "Theirs", SampleSchema), Admin(otherOrg.Id));
        await service.CreateAsync(new CreateClinicalNoteTemplateRequest(NoteType.Daily, TemplateScope.Platform, null, null, "Global", SampleSchema), SuperAdmin());

        var list = await service.ListAsync(Admin(org.Id));
        Assert.Equal(2, list.Count);
        Assert.Contains(list, t => t.Name == "Mine");
        Assert.Contains(list, t => t.Name == "Global");
        Assert.DoesNotContain(list, t => t.Name == "Theirs");
    }
}
