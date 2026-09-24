using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Consents;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Structural mirror of ClinicalTemplateServiceTests -- same two
/// real jobs (versioning, most-specific-wins resolution), same rules, just
/// exercised through the consent-template engine instead.</summary>
public class ConsentTemplateServiceTests
{
    private static (PhysioTracDbContext Db, ConsentTemplateService Service, Organization Org, Location Location)
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
        var service = new ConsentTemplateService(db, tenantAccess, audit);
        return (db, service, org, location);
    }

    private static TestCurrentUser Admin(Guid orgId) => new() { UserId = Guid.NewGuid(), OrganizationId = orgId, Role = UserRole.Admin };
    private static TestCurrentUser SuperAdmin() => new() { UserId = Guid.NewGuid(), OrganizationId = null, Role = UserRole.SuperAdmin, IsPlatformSuperAdmin = true };

    [Fact]
    public async Task Create_OrganizationScope_StartsAtVersion1()
    {
        var (db, service, org, _) = NewService();
        var template = await service.CreateAsync(
            new CreateConsentTemplateRequest(ConsentType.ConsentToTreat, TemplateScope.Organization, null, null, "I consent to treatment."), Admin(org.Id));

        Assert.Equal(1, template.Version);
        Assert.True(template.IsActive);
    }

    [Fact]
    public async Task Create_SecondVersionForTheSameScope_DeactivatesThePrevious_AndIncrementsVersion()
    {
        var (db, service, org, _) = NewService();
        var admin = Admin(org.Id);
        var v1 = await service.CreateAsync(
            new CreateConsentTemplateRequest(ConsentType.ConsentToTreat, TemplateScope.Organization, null, null, "v1 text"), admin);
        var v2 = await service.CreateAsync(
            new CreateConsentTemplateRequest(ConsentType.ConsentToTreat, TemplateScope.Organization, null, null, "v2 text"), admin);

        Assert.Equal(2, v2.Version);
        var reloadedV1 = await db.ConsentTemplates.FindAsync(v1.Id);
        Assert.False(reloadedV1!.IsActive);
        Assert.Equal(2, await db.ConsentTemplates.CountAsync());
    }

    [Fact]
    public async Task Create_BlankBodyText_Throws()
    {
        var (_, service, org, _) = NewService();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            new CreateConsentTemplateRequest(ConsentType.ConsentToTreat, TemplateScope.Organization, null, null, "   "), Admin(org.Id)));
    }

    [Fact]
    public async Task Create_StateScope_RequiresATwoLetterCode()
    {
        var (_, service, org, _) = NewService();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            new CreateConsentTemplateRequest(ConsentType.ConsentToTreat, TemplateScope.State, "North Carolina", null, "Bad state"), Admin(org.Id)));
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
            new CreateConsentTemplateRequest(ConsentType.ConsentToTreat, TemplateScope.Location, null, otherLocation.Id, "Bad location"), Admin(org.Id)));
    }

    [Fact]
    public async Task Create_PlatformScope_RequiresPlatformSuperAdmin_NotJustOrgAdmin()
    {
        var (_, service, org, _) = NewService();
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(
            new CreateConsentTemplateRequest(ConsentType.ConsentToTreat, TemplateScope.Platform, null, null, "Global default"), Admin(org.Id)));
    }

    [Fact]
    public async Task Resolve_PrefersLocation_ThenState_ThenOrganization_ThenPlatform()
    {
        var (db, service, org, location) = NewService();
        var admin = Admin(org.Id);

        var platformTemplate = await service.CreateAsync(
            new CreateConsentTemplateRequest(ConsentType.ConsentToTreat, TemplateScope.Platform, null, null, "Platform default"), SuperAdmin());

        var resolved1 = await service.ResolveAsync(admin, ConsentType.ConsentToTreat, location.Id, "NC");
        Assert.Equal(platformTemplate.Id, resolved1!.Id);

        var orgTemplate = await service.CreateAsync(
            new CreateConsentTemplateRequest(ConsentType.ConsentToTreat, TemplateScope.Organization, null, null, "Org default"), admin);
        var resolved2 = await service.ResolveAsync(admin, ConsentType.ConsentToTreat, location.Id, "NC");
        Assert.Equal(orgTemplate.Id, resolved2!.Id);

        var stateTemplate = await service.CreateAsync(
            new CreateConsentTemplateRequest(ConsentType.ConsentToTreat, TemplateScope.State, "NC", null, "NC template"), admin);
        var resolved3 = await service.ResolveAsync(admin, ConsentType.ConsentToTreat, location.Id, "NC");
        Assert.Equal(stateTemplate.Id, resolved3!.Id);

        var locationTemplate = await service.CreateAsync(
            new CreateConsentTemplateRequest(ConsentType.ConsentToTreat, TemplateScope.Location, null, location.Id, "Location template"), admin);
        var resolved4 = await service.ResolveAsync(admin, ConsentType.ConsentToTreat, location.Id, "NC");
        Assert.Equal(locationTemplate.Id, resolved4!.Id);
    }

    [Fact]
    public async Task Resolve_ReturnsNull_WhenNothingIsConfiguredAtAnyLevel()
    {
        var (_, service, org, location) = NewService();
        var resolved = await service.ResolveAsync(Admin(org.Id), ConsentType.TelehealthConsent, location.Id, "NC");
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
            new CreateConsentTemplateRequest(ConsentType.ConsentToTreat, TemplateScope.Organization, null, null, "Other org's template"),
            Admin(otherOrg.Id));

        var resolved = await service.ResolveAsync(Admin(org.Id), ConsentType.ConsentToTreat, null, null);
        Assert.Null(resolved);
    }
}
