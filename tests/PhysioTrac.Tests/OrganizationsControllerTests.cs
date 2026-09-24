using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Api.Controllers;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

public class OrganizationsControllerTests
{
    private static PhysioTracDbContext NewDb() => new(
        new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static OrganizationsController NewController(PhysioTracDbContext db, TestCurrentUser user)
    {
        var audit = new AuditService(db);
        var controller = new OrganizationsController(new TenantAccessService(db, audit), user, audit, db);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    [Fact]
    public async Task Current_ReturnsCallersOwnOrganization_WithItsActiveLocations_ExcludingInactiveAndOtherOrgs()
    {
        var db = NewDb();
        var org = new Organization { Name = "Source Motion Physical Therapy", Slug = "source-motion-pt" };
        var otherOrg = new Organization { Name = "Total Motion PT", Slug = "total-motion-pt" };
        var active = new Location { OrganizationId = org.Id, Name = "Fuquay-Varina", IsActive = true };
        var inactive = new Location { OrganizationId = org.Id, Name = "Closed Branch", IsActive = false };
        var otherOrgLocation = new Location { OrganizationId = otherOrg.Id, Name = "Austin", IsActive = true };
        db.Organizations.AddRange(org, otherOrg);
        db.Locations.AddRange(active, inactive, otherOrgLocation);
        await db.SaveChangesAsync();

        var user = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        var controller = NewController(db, user);

        var result = Assert.IsType<OkObjectResult>(await controller.Current());
        var dto = Assert.IsType<CurrentOrganizationDto>(result.Value);

        Assert.Equal(org.Id, dto.Id);
        Assert.Equal("Source Motion Physical Therapy", dto.Name);
        var location = Assert.Single(dto.Locations);
        Assert.Equal("Fuquay-Varina", location.Name);
    }

    [Fact]
    public async Task UpdateProfile_Admin_UpdatesOwnOrganization_LeavesOtherOrganizationsUntouched_AndWritesAuditEvent()
    {
        var db = NewDb();
        var org = new Organization { Name = "Source Motion Physical Therapy", Slug = "source-motion-pt", NpiNumber = "1111111111" };
        var otherOrg = new Organization { Name = "Total Motion PT", Slug = "total-motion-pt", NpiNumber = "2222222222" };
        db.Organizations.AddRange(org, otherOrg);
        await db.SaveChangesAsync();

        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        var controller = NewController(db, admin);

        var request = new UpdateOrganizationProfileRequest(
            "Source Motion Physical Therapy (Rebrand)", "America/New_York", "9999999999", null,
            null, null, null, null, null, "new-support@sourcemotionpt.test", null, true);

        var result = Assert.IsType<OkObjectResult>(await controller.UpdateProfile(request));
        var dto = Assert.IsType<OrganizationProfileDto>(result.Value);
        Assert.Equal("Source Motion Physical Therapy (Rebrand)", dto.Name);
        Assert.Equal("9999999999", dto.NpiNumber);

        var untouchedOtherOrg = await db.Organizations.FindAsync(otherOrg.Id);
        Assert.Equal("Total Motion PT", untouchedOtherOrg!.Name);
        Assert.Equal("2222222222", untouchedOtherOrg.NpiNumber);

        var auditEvents = await db.AuditEvents.Where(e => e.ObjectId == org.Id && e.Action == "entity.updated").ToListAsync();
        Assert.Single(auditEvents);
    }

    [Fact]
    public async Task UpdateProfile_NonAdminRole_Returns403_AndChangesNothing()
    {
        var db = NewDb();
        var org = new Organization { Name = "Source Motion Physical Therapy", Slug = "source-motion-pt" };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        // Therapist is Clinical but not OrganizationAdministration.
        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist };
        var controller = NewController(db, therapist);

        var request = new UpdateOrganizationProfileRequest(
            "Hacked Name", null, null, null, null, null, null, null, null, null, null, true);

        var result = Assert.IsType<ObjectResult>(await controller.UpdateProfile(request));
        Assert.Equal(403, result.StatusCode);

        var unchanged = await db.Organizations.FindAsync(org.Id);
        Assert.Equal("Source Motion Physical Therapy", unchanged!.Name);
    }
}
