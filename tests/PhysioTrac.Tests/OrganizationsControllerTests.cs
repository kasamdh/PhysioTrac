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
        var controller = new OrganizationsController(new TenantAccessService(db, new AuditService(db)), user, db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = Assert.IsType<OkObjectResult>(await controller.Current());
        var dto = Assert.IsType<CurrentOrganizationDto>(result.Value);

        Assert.Equal(org.Id, dto.Id);
        Assert.Equal("Source Motion Physical Therapy", dto.Name);
        var location = Assert.Single(dto.Locations);
        Assert.Equal("Fuquay-Varina", location.Name);
    }
}
