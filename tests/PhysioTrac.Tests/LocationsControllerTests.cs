using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Api.Controllers;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

public class LocationsControllerTests
{
    private static PhysioTracDbContext NewDb() => new(
        new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static LocationsController NewController(PhysioTracDbContext db, TestCurrentUser user)
    {
        var tenantAccess = new TenantAccessService(db, new AuditService(db));
        var controller = new LocationsController(tenantAccess, user, db);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static async Task<(PhysioTracDbContext Db, Organization Org1000, Organization Org1001, Location LocationIn1001)> SeedAsync()
    {
        var db = NewDb();
        var org1000 = new Organization { Name = "Org 1000", Slug = "org-1000" };
        var org1001 = new Organization { Name = "Org 1001", Slug = "org-1001" };
        var locationIn1001 = new Location { OrganizationId = org1001.Id, Name = "Austin" };
        db.Organizations.AddRange(org1000, org1001);
        db.Locations.Add(locationIn1001);
        await db.SaveChangesAsync();
        return (db, org1000, org1001, locationIn1001);
    }

    [Fact]
    public async Task Admin_CreatesLocation_WithNpiAndTaxId()
    {
        var (db, org1000, _, _) = await SeedAsync();
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        var controller = NewController(db, admin);

        var result = Assert.IsType<CreatedAtActionResult>(await controller.Create(
            new CreateLocationRequest("Raleigh", "500 Wellness Blvd", null, "Raleigh", "NC", "27601", "919-555-0199", "America/New_York", "1234567890", "98-7654321")));
        var dto = Assert.IsType<LocationDto>(result.Value);

        Assert.Equal("1234567890", dto.NpiNumber);
        Assert.Equal("98-7654321", dto.TaxId);

        var persisted = await db.Locations.FindAsync(dto.Id);
        Assert.Equal(org1000.Id, persisted!.OrganizationId);
    }

    [Fact]
    public async Task Therapist_CreatingLocation_Returns403()
    {
        // Therapist is Clinical/Scheduling but not OrganizationAdministration.
        var (db, org1000, _, _) = await SeedAsync();
        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Therapist };
        var controller = NewController(db, therapist);

        var result = Assert.IsType<ObjectResult>(await controller.Create(
            new CreateLocationRequest("Raleigh", null, null, null, null, null, null, null, null, null)));
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task Scheduler_CanListLocations_ButCannotCreateOne()
    {
        var (db, org1000, _, _) = await SeedAsync();
        db.Locations.Add(new Location { OrganizationId = org1000.Id, Name = "Fuquay-Varina" });
        await db.SaveChangesAsync();

        var scheduler = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Scheduler };
        var controller = NewController(db, scheduler);

        var listResult = Assert.IsType<OkObjectResult>(await controller.List(includeInactive: false));
        var locations = Assert.IsAssignableFrom<IEnumerable<LocationDto>>(listResult.Value);
        Assert.Single(locations);

        var createResult = Assert.IsType<ObjectResult>(await controller.Create(
            new CreateLocationRequest("New Site", null, null, null, null, null, null, null, null, null)));
        Assert.Equal(403, createResult.StatusCode);
    }

    [Fact]
    public async Task Org1000Admin_Reading_Org1001Location_Returns404()
    {
        var (db, org1000, _, locationIn1001) = await SeedAsync();
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        var controller = NewController(db, admin);

        var result = await controller.Get(locationIn1001.Id);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Org1000Admin_Updating_Org1001Location_Returns404_AndNeverWritesTheChange()
    {
        var (db, org1000, _, locationIn1001) = await SeedAsync();
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        var controller = NewController(db, admin);

        var result = await controller.Update(locationIn1001.Id,
            new UpdateLocationRequest("Hacked", null, null, null, null, null, null, null, null, null));
        Assert.IsType<NotFoundObjectResult>(result);

        var unchanged = await db.Locations.FindAsync(locationIn1001.Id);
        Assert.Equal("Austin", unchanged!.Name);
    }

    [Fact]
    public async Task Deactivate_SetsIsActiveFalse_ExcludedFromDefaultList_ButVisibleWithIncludeInactive()
    {
        var (db, org1000, _, _) = await SeedAsync();
        var location = new Location { OrganizationId = org1000.Id, Name = "Fuquay-Varina" };
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        var controller = NewController(db, admin);

        Assert.IsType<OkObjectResult>(await controller.Deactivate(location.Id));

        var defaultList = Assert.IsType<OkObjectResult>(await controller.List(includeInactive: false));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<LocationDto>>(defaultList.Value));

        var fullList = Assert.IsType<OkObjectResult>(await controller.List(includeInactive: true));
        Assert.Single(Assert.IsAssignableFrom<IEnumerable<LocationDto>>(fullList.Value));
    }
}
