using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PhysioTrac.Api.Controllers;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

public class RoomsControllerTests
{
    private static PhysioTracDbContext NewDb() => new(
        new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Create_ThenList_RoundTrips_AndDeactivateExcludesFromDefaultList()
    {
        var db = NewDb();
        var org = new Organization { Name = "Org 1000", Slug = "org-1000" };
        var location = new Location { OrganizationId = org.Id, Name = "Fuquay-Varina" };
        db.Organizations.Add(org);
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        var controller = new RoomsController(new TenantAccessService(db, new AuditService(db)), admin, db, new AuditService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var created = Assert.IsType<CreatedAtActionResult>(await controller.Create(location.Id, new CreateRoomRequest("Treatment Room 1")));
        var dto = Assert.IsType<RoomDto>(created.Value);

        await controller.Deactivate(location.Id, dto.Id);

        var defaultList = Assert.IsType<OkObjectResult>(await controller.List(location.Id));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<RoomDto>>(defaultList.Value));
    }

    [Fact]
    public async Task Create_ForAnotherOrganizationsLocation_ThrowsNotFound()
    {
        var db = NewDb();
        var org1000 = new Organization { Name = "Org 1000", Slug = "org-1000" };
        var org1001 = new Organization { Name = "Org 1001", Slug = "org-1001" };
        var otherOrgLocation = new Location { OrganizationId = org1001.Id, Name = "Austin" };
        db.Organizations.AddRange(org1000, org1001);
        db.Locations.Add(otherOrgLocation);
        await db.SaveChangesAsync();

        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        var controller = new RoomsController(new TenantAccessService(db, new AuditService(db)), admin, db, new AuditService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.Create(otherOrgLocation.Id, new CreateRoomRequest("Should Fail"));
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Create_ByTherapistRole_Returns403_NotOrganizationAdministration()
    {
        var db = NewDb();
        var org = new Organization { Name = "Org 1000", Slug = "org-1000" };
        var location = new Location { OrganizationId = org.Id, Name = "Fuquay-Varina" };
        db.Organizations.Add(org);
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        var therapist = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Therapist };
        var controller = new RoomsController(new TenantAccessService(db, new AuditService(db)), therapist, db, new AuditService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = Assert.IsType<ObjectResult>(await controller.Create(location.Id, new CreateRoomRequest("Room")));
        Assert.Equal(403, result.StatusCode);
    }
}

public class WaitlistControllerTests
{
    private static PhysioTracDbContext NewDb() => new(
        new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static WaitlistController NewController(PhysioTracDbContext db, TestCurrentUser user)
    {
        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var appointments = new AppointmentService(db, tenantAccess, audit, new NoOpReminderService(NullLogger<NoOpReminderService>.Instance));
        var controller = new WaitlistController(tenantAccess, user, appointments, db);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static async Task<(PhysioTracDbContext Db, Organization Org, Patient Patient)> SeedAsync()
    {
        var db = NewDb();
        var org = new Organization { Name = "Org 1000", Slug = "org-1000" };
        var patient = new Patient { OrganizationId = org.Id, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1) };
        db.Organizations.Add(org);
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return (db, org, patient);
    }

    [Fact]
    public async Task Create_ThenList_RoundTrips()
    {
        var (db, org, patient) = await SeedAsync();
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        var controller = NewController(db, admin);

        await controller.Create(new CreateWaitlistEntryRequest(patient.Id, null, null, null, DateOnly.FromDateTime(DateTime.Today), null, "Prefers mornings"));
        var result = Assert.IsType<OkObjectResult>(await controller.List());
        var entries = Assert.IsAssignableFrom<IEnumerable<WaitlistDto>>(result.Value);
        Assert.Single(entries);
    }

    [Fact]
    public async Task Convert_BooksARealAppointment_AndMarksTheEntryFulfilled()
    {
        var (db, org, patient) = await SeedAsync();
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        var controller = NewController(db, admin);
        var created = Assert.IsType<CreatedAtActionResult>(await controller.Create(
            new CreateWaitlistEntryRequest(patient.Id, null, null, null, DateOnly.FromDateTime(DateTime.Today), null, null)));
        var entry = Assert.IsType<WaitlistDto>(created.Value);

        var start = DateTimeOffset.UtcNow.AddDays(1);
        var result = Assert.IsType<OkObjectResult>(await controller.Convert(entry.Id,
            new ConvertWaitlistEntryRequest(Guid.NewGuid(), null, null, AppointmentKind.FollowUp, start, start.AddMinutes(30))));

        Assert.NotNull(await db.Appointments.FirstOrDefaultAsync());
        var listResult = Assert.IsType<OkObjectResult>(await controller.List(WaitlistStatus.Fulfilled));
        Assert.Single(Assert.IsAssignableFrom<IEnumerable<WaitlistDto>>(listResult.Value));
    }

    [Fact]
    public async Task Create_ForAnotherOrganizationsPatient_Returns403()
    {
        var (db, org, _) = await SeedAsync();
        var otherOrg = new Organization { Name = "Org 1001", Slug = "org-1001" };
        var otherPatient = new Patient { OrganizationId = otherOrg.Id, FirstName = "Other", LastName = "Org", DateOfBirth = new DateOnly(1980, 1, 1) };
        db.Organizations.Add(otherOrg);
        db.Patients.Add(otherPatient);
        await db.SaveChangesAsync();

        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        var controller = NewController(db, admin);

        var result = Assert.IsType<ObjectResult>(await controller.Create(
            new CreateWaitlistEntryRequest(otherPatient.Id, null, null, null, DateOnly.FromDateTime(DateTime.Today), null, null)));
        Assert.Equal(403, result.StatusCode);
    }
}
