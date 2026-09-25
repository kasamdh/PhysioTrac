using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

public class PlatformDashboardServiceTests
{
    private static (PhysioTracDbContext Db, PlatformDashboardService Service) NewService()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        return (db, new PlatformDashboardService(db, tenantAccess));
    }

    private static TestCurrentUser SuperAdmin() => new() { UserId = Guid.NewGuid(), OrganizationId = null, Role = UserRole.SuperAdmin, IsPlatformSuperAdmin = true };
    private static TestCurrentUser Admin(Guid orgId) => new() { UserId = Guid.NewGuid(), OrganizationId = orgId, Role = UserRole.Admin };

    [Fact]
    public async Task GetOrganizationPerformance_ByNonSuperAdmin_ThrowsForbidden()
    {
        var (db, service) = NewService();
        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetOrganizationPerformanceAsync(Admin(org.Id), DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [Fact]
    public async Task GetOrganizationPerformance_AggregatesAcrossTenants_KeepingEachOrganizationsNumbersSeparate()
    {
        var (db, service) = NewService();
        var orgA = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        var orgB = new Organization { Name = "Client B", Slug = "client-b", ClientNumber = 1001 };
        db.Organizations.AddRange(orgA, orgB);
        await db.SaveChangesAsync();

        var patientA = new Patient { OrganizationId = orgA.Id, FirstName = "A", LastName = "One", DateOfBirth = new DateOnly(1990, 1, 1), CreatedAt = DateTimeOffset.UtcNow };
        var patientB = new Patient { OrganizationId = orgB.Id, FirstName = "B", LastName = "One", DateOfBirth = new DateOnly(1990, 1, 1), CreatedAt = DateTimeOffset.UtcNow };
        db.Patients.AddRange(patientA, patientB);
        await db.SaveChangesAsync();

        db.Charges.AddRange(
            new Charge { OrganizationId = orgA.Id, PatientId = patientA.Id, ProviderId = Guid.NewGuid(), ServiceDate = DateOnly.FromDateTime(DateTime.UtcNow), CptCode = "97110", ChargeAmount = 100m },
            new Charge { OrganizationId = orgB.Id, PatientId = patientB.Id, ProviderId = Guid.NewGuid(), ServiceDate = DateOnly.FromDateTime(DateTime.UtcNow), CptCode = "97110", ChargeAmount = 250m });
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await service.GetOrganizationPerformanceAsync(SuperAdmin(), today, today);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(100m, Assert.Single(result.Rows, r => r.OrganizationId == orgA.Id).Revenue);
        Assert.Equal(250m, Assert.Single(result.Rows, r => r.OrganizationId == orgB.Id).Revenue);
    }

    [Fact]
    public async Task GetOrganizationPerformance_ExcludesArchivedOrganizations()
    {
        var (db, service) = NewService();
        var archived = new Organization { Name = "Gone", Slug = "gone", ClientNumber = 1000, ArchivedAt = DateTimeOffset.UtcNow };
        db.Organizations.Add(archived);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await service.GetOrganizationPerformanceAsync(SuperAdmin(), today, today);

        Assert.Empty(result.Rows);
    }
}
