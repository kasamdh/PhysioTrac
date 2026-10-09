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

    private static TestCurrentUser As(Organization org, UserRole role) =>
        new() { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = role };

    private static string Detail(IActionResult result)
    {
        var value = ((ObjectResult)result).Value!;
        return value.GetType().GetProperty("detail")!.GetValue(value)!.ToString()!;
    }

    [Fact]
    public async Task Create_SavesDisciplineOnlineBookingAndLogin_AndTrimsFields()
    {
        var (db, org1000, _, _, _, _) = await SeedAsync();
        var login = TestTherapists.Add(db, org1000.Id, UserRole.Assistant);
        var controller = NewController(db, As(org1000, UserRole.Scheduler));

        var result = Assert.IsType<CreatedAtActionResult>(await controller.Create(new CreateProviderRequest(
            "  Avery ", "Kim ", " ", "PTA", null, login, null, ProviderDiscipline.PTA, OnlineBookingEnabled: false)));
        var dto = Assert.IsType<ProviderDto>(result.Value);

        Assert.Equal(("Avery", "Kim", (string?)null, ProviderDiscipline.PTA, false, login),
            (dto.FirstName, dto.LastName, dto.Specialty, dto.Discipline, dto.OnlineBookingEnabled, dto.UserId!.Value));
        Assert.True(dto.HasLogin);
    }

    [Fact]
    public async Task Create_RefusesInvalidInput()
    {
        var (db, org1000, org1001, _, _, _) = await SeedAsync();
        var controller = NewController(db, As(org1000, UserRole.Scheduler));

        Assert.Equal(422, ((ObjectResult)await controller.Create(new CreateProviderRequest(" ", "Chen", null, null, null, null, null))).StatusCode);
        Assert.Equal(422, ((ObjectResult)await controller.Create(new CreateProviderRequest("Jamie", "Chen", null, null, "12345", null, null))).StatusCode);

        Assert.IsType<CreatedAtActionResult>(await controller.Create(new CreateProviderRequest("Jamie", "Chen", null, null, "1234567890", null, null)));
        var duplicate = await controller.Create(new CreateProviderRequest("Sam", "Lee", null, null, "1234567890", null, null));
        Assert.Equal(409, ((ObjectResult)duplicate).StatusCode);
        Assert.Contains("NPI", Detail(duplicate));

        // A login from another clinic, a patient login, or one already linked.
        var otherClinicLogin = TestTherapists.Add(db, org1001.Id);
        Assert.Equal(422, ((ObjectResult)await controller.Create(new CreateProviderRequest("A", "B", null, null, null, otherClinicLogin, null))).StatusCode);
        var patientLogin = TestTherapists.Add(db, org1000.Id, UserRole.Patient);
        Assert.Equal(422, ((ObjectResult)await controller.Create(new CreateProviderRequest("A", "B", null, null, null, patientLogin, null))).StatusCode);
        var login = TestTherapists.Add(db, org1000.Id);
        Assert.IsType<CreatedAtActionResult>(await controller.Create(new CreateProviderRequest("C", "D", null, null, null, login, null)));
        Assert.Equal(409, ((ObjectResult)await controller.Create(new CreateProviderRequest("E", "F", null, null, null, login, null))).StatusCode);
    }

    [Fact]
    public async Task Update_ChangesOrUnlinksTheLogin_OnlyWhenAsked()
    {
        var (db, org1000, _, _, _, _) = await SeedAsync();
        var login = TestTherapists.Add(db, org1000.Id);
        var provider = new Provider { OrganizationId = org1000.Id, FirstName = "Jamie", LastName = "Chen", UserId = login, Discipline = ProviderDiscipline.PT };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
        var controller = NewController(db, As(org1000, UserRole.Scheduler));

        // A caller that doesn't send the login keeps it, and the discipline.
        var kept = (ProviderDto)((OkObjectResult)await controller.Update(provider.Id,
            new UpdateProviderRequest("Jamie", "Chen", null, null, null, false, null))).Value!;
        Assert.Equal((login, ProviderDiscipline.PT, false), (kept.UserId!.Value, kept.Discipline, kept.IsActive));

        var unlinked = (ProviderDto)((OkObjectResult)await controller.Update(provider.Id,
            new UpdateProviderRequest("Jamie", "Chen", null, null, null, true, null, UpdateLogin: true, UserId: null))).Value!;
        Assert.False(unlinked.HasLogin);
    }

    [Fact]
    public async Task Delete_RemovesAnUnusedProvider_WithTheirHours()
    {
        var (db, org1000, _, locationA, _, _) = await SeedAsync();
        var provider = new Provider { OrganizationId = org1000.Id, FirstName = "Added", LastName = "ByMistake" };
        provider.Locations.Add(locationA);
        db.Providers.Add(provider);
        db.ProviderAvailabilities.Add(new ProviderAvailability { ProviderId = provider.Id, LocationId = locationA.Id, DayOfWeek = Weekday.Monday,
            StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) });
        await db.SaveChangesAsync();

        Assert.IsType<NoContentResult>(await NewController(db, As(org1000, UserRole.Admin)).Delete(provider.Id));
        Assert.False(await db.Providers.AnyAsync(p => p.Id == provider.Id));
        Assert.False(await db.ProviderAvailabilities.AnyAsync(a => a.ProviderId == provider.Id));
    }

    [Fact]
    public async Task Delete_RefusesAProviderWithHistory_AndOtherClinicsAndSchedulers()
    {
        var (db, org1000, _, _, _, providerIn1001) = await SeedAsync();
        var patient = new Patient { OrganizationId = org1000.Id, FirstName = "Taylor", LastName = "Sample", DateOfBirth = new DateOnly(1980, 1, 1) };
        var provider = new Provider { OrganizationId = org1000.Id, FirstName = "Jamie", LastName = "Chen" };
        db.Patients.Add(patient);
        db.Providers.Add(provider);
        var at = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);
        db.Appointments.Add(new Appointment { PatientId = patient.Id, ProviderId = provider.Id, TherapistId = Guid.NewGuid(), StartsAt = at, EndsAt = at.AddMinutes(45) });
        await db.SaveChangesAsync();
        var admin = NewController(db, As(org1000, UserRole.Admin));

        var refused = await admin.Delete(provider.Id);
        Assert.Equal(409, ((ObjectResult)refused).StatusCode);
        Assert.Contains("Deactivate them instead", Detail(refused));
        Assert.True(await db.Providers.AnyAsync(p => p.Id == provider.Id));

        Assert.IsType<NotFoundObjectResult>(await admin.Delete(providerIn1001.Id));
        Assert.Equal(403, ((ObjectResult)await NewController(db, As(org1000, UserRole.Scheduler)).Delete(provider.Id)).StatusCode);
    }

    [Fact]
    public async Task LinkableUsers_AreThisClinicsUnlinkedClinicalLogins()
    {
        var (db, org1000, org1001, _, _, _) = await SeedAsync();
        var free = TestTherapists.Add(db, org1000.Id, UserRole.Assistant);
        var taken = TestTherapists.Add(db, org1000.Id);
        var mine = TestTherapists.Add(db, org1000.Id);
        TestTherapists.Add(db, org1000.Id, UserRole.Biller);
        TestTherapists.Add(db, org1000.Id, UserRole.Patient);
        TestTherapists.Add(db, org1001.Id);
        db.Providers.Add(new Provider { OrganizationId = org1000.Id, FirstName = "T", LastName = "Aken", UserId = taken });
        var editing = new Provider { OrganizationId = org1000.Id, FirstName = "M", LastName = "Ine", UserId = mine };
        db.Providers.Add(editing);
        await db.SaveChangesAsync();

        var result = (OkObjectResult)await NewController(db, As(org1000, UserRole.Scheduler)).LinkableUsers(editing.Id);
        var ids = ((IEnumerable<LinkableUserDto>)result.Value!).Select(u => u.Id).ToHashSet();
        Assert.Equal(new HashSet<Guid> { free, mine }, ids);
    }
}
