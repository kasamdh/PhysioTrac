using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Api.Controllers;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

public class ProvidersControllerTests
{
    private static PhysioTracDbContext NewDb() => new(
        new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ProvidersController NewController(PhysioTracDbContext db, TestCurrentUser user)
    {
        var tenantAccess = new TenantAccessService(db, new AuditService(db));
        var controller = new ProvidersController(tenantAccess, user, db);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static async Task<(PhysioTracDbContext Db, Organization Org1000, Organization Org1001, Location LocationA, Location LocationB, Provider ProviderIn1001)> SeedAsync()
    {
        var db = NewDb();
        var org1000 = new Organization { Name = "Org 1000", Slug = "org-1000" };
        var org1001 = new Organization { Name = "Org 1001", Slug = "org-1001" };
        var locationA = new Location { OrganizationId = org1000.Id, Name = "Fuquay-Varina" };
        var locationB = new Location { OrganizationId = org1000.Id, Name = "Raleigh" };
        var locationInOtherOrg = new Location { OrganizationId = org1001.Id, Name = "Austin" };
        var providerIn1001 = new Provider { OrganizationId = org1001.Id, FirstName = "Priya", LastName = "Sharma" };
        db.Organizations.AddRange(org1000, org1001);
        db.Locations.AddRange(locationA, locationB, locationInOtherOrg);
        db.Providers.Add(providerIn1001);
        await db.SaveChangesAsync();
        return (db, org1000, org1001, locationA, locationB, providerIn1001);
    }

    [Fact]
    public async Task Create_WithNpiAndLocations_PersistsBoth()
    {
        var (db, org1000, _, locationA, locationB, _) = await SeedAsync();
        var scheduler = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Scheduler };
        var controller = NewController(db, scheduler);

        var result = Assert.IsType<CreatedAtActionResult>(await controller.Create(
            new CreateProviderRequest("Jamie", "Chen", "Orthopedic", "PT, DPT", "1234567890", null, [locationA.Id, locationB.Id])));
        var dto = Assert.IsType<ProviderDto>(result.Value);

        Assert.Equal("1234567890", dto.NpiNumber);
        Assert.Equal(2, dto.LocationIds.Count);
    }

    [Fact]
    public async Task Create_IgnoresLocationIdFromAnotherOrganization()
    {
        var (db, org1000, _, locationA, _, _) = await SeedAsync();
        var otherOrgLocation = await db.Locations.FirstAsync(l => l.Name == "Austin");
        var scheduler = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Scheduler };
        var controller = NewController(db, scheduler);

        var result = Assert.IsType<CreatedAtActionResult>(await controller.Create(
            new CreateProviderRequest("Jamie", "Chen", null, null, null, null, [locationA.Id, otherOrgLocation.Id])));
        var dto = Assert.IsType<ProviderDto>(result.Value);

        var onlyLocation = Assert.Single(dto.LocationIds);
        Assert.Equal(locationA.Id, onlyLocation);
    }

    [Fact]
    public async Task Update_ReassignsLocations_ReplacingThePreviousSet()
    {
        var (db, org1000, _, locationA, locationB, _) = await SeedAsync();
        var provider = new Provider { OrganizationId = org1000.Id, FirstName = "Jamie", LastName = "Chen" };
        provider.Locations.Add(locationA);
        db.Providers.Add(provider);
        await db.SaveChangesAsync();

        var scheduler = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Scheduler };
        var controller = NewController(db, scheduler);

        var result = Assert.IsType<OkObjectResult>(await controller.Update(provider.Id,
            new UpdateProviderRequest("Jamie", "Chen", "Orthopedic", "PT, DPT", "1234567890", true, [locationB.Id])));
        var dto = Assert.IsType<ProviderDto>(result.Value);

        var onlyLocation = Assert.Single(dto.LocationIds);
        Assert.Equal(locationB.Id, onlyLocation);
    }

    [Fact]
    public async Task Org1000User_Updating_Org1001Provider_Returns404()
    {
        var (db, org1000, _, _, _, providerIn1001) = await SeedAsync();
        var scheduler = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Scheduler };
        var controller = NewController(db, scheduler);

        var result = await controller.Update(providerIn1001.Id,
            new UpdateProviderRequest("Hacked", "Name", null, null, null, true, null));
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Biller_CreatingProvider_Returns403()
    {
        // Biller is not in RoleSets.Scheduling.
        var (db, org1000, _, _, _, _) = await SeedAsync();
        var biller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Biller };
        var controller = NewController(db, biller);

        var result = Assert.IsType<ObjectResult>(await controller.Create(
            new CreateProviderRequest("New", "Provider", null, null, null, null, null)));
        Assert.Equal(403, result.StatusCode);
    }
}
