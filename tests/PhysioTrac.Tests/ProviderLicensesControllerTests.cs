using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Api.Controllers;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

public class ProviderLicenseExpirationAlertLevelTests
{
    private static ProviderLicense LicenseExpiringIn(int days, ProviderLicenseStatus status = ProviderLicenseStatus.Active) => new()
    {
        ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(days),
        Status = status,
    };

    [Theory]
    [InlineData(91, LicenseExpirationAlertLevel.None)]
    [InlineData(90, LicenseExpirationAlertLevel.Notice90)]
    [InlineData(61, LicenseExpirationAlertLevel.Notice90)]
    [InlineData(60, LicenseExpirationAlertLevel.Notice60)]
    [InlineData(31, LicenseExpirationAlertLevel.Notice60)]
    [InlineData(30, LicenseExpirationAlertLevel.Notice30)]
    [InlineData(1, LicenseExpirationAlertLevel.Notice30)]
    [InlineData(-1, LicenseExpirationAlertLevel.Expired)]
    public void ExpirationAlertLevel_MatchesTheExpectedTier(int daysUntilExpiration, LicenseExpirationAlertLevel expected)
    {
        var license = LicenseExpiringIn(daysUntilExpiration);
        Assert.Equal(expected, license.ExpirationAlertLevel);
    }

    [Fact]
    public void ExpirationAlertLevel_IsNone_ForAnInactiveLicense_EvenIfPastItsExpirationDate()
    {
        var license = LicenseExpiringIn(-30, ProviderLicenseStatus.Revoked);
        Assert.Equal(LicenseExpirationAlertLevel.None, license.ExpirationAlertLevel);
    }
}

public class ProviderLicensesControllerTests
{
    private static PhysioTracDbContext NewDb() => new(
        new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ProviderLicensesController NewController(PhysioTracDbContext db, TestCurrentUser user)
    {
        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var controller = new ProviderLicensesController(tenantAccess, user, audit, db);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static async Task<(PhysioTracDbContext Db, Organization Org, Provider Provider)> SeedAsync()
    {
        var db = NewDb();
        var org = new Organization { Name = "Org 1000", Slug = "org-1000" };
        var provider = new Provider { OrganizationId = org.Id, FirstName = "Jamie", LastName = "Chen" };
        db.Organizations.Add(org);
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
        return (db, org, provider);
    }

    [Fact]
    public async Task Create_WritesAnExplicitAuditEvent_SinceProviderLicenseHasNoDirectOrganizationId()
    {
        var (db, org, provider) = await SeedAsync();
        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        var controller = NewController(db, admin);

        var result = Assert.IsType<CreatedAtActionResult>(await controller.Create(provider.Id,
            new CreateProviderLicenseRequest("NC", "NC-PT-12345", null, DateOnly.FromDateTime(DateTime.Today.AddYears(2)), ProviderLicenseStatus.Active, false, null)));
        var dto = Assert.IsType<ProviderLicenseDto>(result.Value);

        var auditEvent = await db.AuditEvents.SingleAsync(e => e.ObjectId == dto.Id && e.Action == "entity.created");
        Assert.Equal(org.Id, auditEvent.OrganizationId);
        Assert.Equal(nameof(ProviderLicense), auditEvent.ObjectType);
    }

    [Fact]
    public async Task Update_WritesAnExplicitAuditEvent()
    {
        var (db, org, provider) = await SeedAsync();
        var license = new ProviderLicense
        {
            ProviderId = provider.Id, State = "NC", LicenseNumber = "NC-PT-12345",
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddYears(1)),
        };
        db.ProviderLicenses.Add(license);
        await db.SaveChangesAsync();

        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Admin };
        var controller = NewController(db, admin);

        await controller.Update(provider.Id, license.Id,
            new UpdateProviderLicenseRequest(DateOnly.FromDateTime(DateTime.Today.AddYears(2)), ProviderLicenseStatus.Active, "Renewed"));

        var auditEvent = await db.AuditEvents.SingleAsync(e => e.ObjectId == license.Id && e.Action == "entity.updated");
        Assert.Equal(org.Id, auditEvent.OrganizationId);
    }

    [Fact]
    public async Task Biller_CreatingLicense_Returns403()
    {
        // Biller is not in RoleSets.Scheduling, which this action requires.
        var (db, org, provider) = await SeedAsync();
        var biller = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org.Id, Role = UserRole.Biller };
        var controller = NewController(db, biller);

        var result = Assert.IsType<ObjectResult>(await controller.Create(provider.Id,
            new CreateProviderLicenseRequest("NC", "NC-PT-12345", null, DateOnly.FromDateTime(DateTime.Today.AddYears(2)), ProviderLicenseStatus.Active, false, null)));
        Assert.Equal(403, result.StatusCode);
    }
}

public class ExpiringLicensesControllerTests
{
    private static PhysioTracDbContext NewDb() => new(
        new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task List_ReturnsOnlyTheCallersOrganizationsAlerts_SortedByUrgency_ExcludingLicensesNotNearExpiration()
    {
        var db = NewDb();
        var org1000 = new Organization { Name = "Org 1000", Slug = "org-1000" };
        var org1001 = new Organization { Name = "Org 1001", Slug = "org-1001" };
        var providerA = new Provider { OrganizationId = org1000.Id, FirstName = "Jamie", LastName = "Chen" };
        var providerB = new Provider { OrganizationId = org1001.Id, FirstName = "Priya", LastName = "Sharma" };
        db.Organizations.AddRange(org1000, org1001);
        db.Providers.AddRange(providerA, providerB);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.ProviderLicenses.AddRange(
            new ProviderLicense { ProviderId = providerA.Id, State = "NC", LicenseNumber = "1", ExpirationDate = today.AddDays(20) }, // Notice30
            new ProviderLicense { ProviderId = providerA.Id, State = "SC", LicenseNumber = "2", ExpirationDate = today.AddDays(200) }, // None -- excluded
            new ProviderLicense { ProviderId = providerA.Id, State = "VA", LicenseNumber = "3", ExpirationDate = today.AddDays(5) }, // Notice30, most urgent
            new ProviderLicense { ProviderId = providerB.Id, State = "TX", LicenseNumber = "4", ExpirationDate = today.AddDays(1) }); // different org -- excluded
        await db.SaveChangesAsync();

        var admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = org1000.Id, Role = UserRole.Admin };
        var tenantAccess = new TenantAccessService(db, new AuditService(db));
        var controller = new ExpiringLicensesController(tenantAccess, admin, db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = Assert.IsType<OkObjectResult>(await controller.List());
        var alerts = Assert.IsAssignableFrom<IEnumerable<ExpiringLicenseDto>>(result.Value).ToList();

        Assert.Equal(2, alerts.Count);
        Assert.Equal("3", alerts[0].LicenseNumber); // 5 days out, most urgent first
        Assert.Equal("1", alerts[1].LicenseNumber);
        Assert.All(alerts, a => Assert.Equal(providerA.Id, a.ProviderId));
    }
}
