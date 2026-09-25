using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Dashboards;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

public class PlatformDashboardService : IPlatformDashboardService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;

    public PlatformDashboardService(PhysioTracDbContext db, ITenantAccessService tenantAccess)
    {
        _db = db;
        _tenantAccess = tenantAccess;
    }

    public async Task<OrganizationPerformanceDto> GetOrganizationPerformanceAsync(ICurrentUser actor, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        _tenantAccess.RequirePlatformSuperAdmin(actor);
        if (to < from)
        {
            throw new InvalidOperationException("The end of the date range cannot precede the start.");
        }

        var fromInstant = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toInstant = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var organizations = await _db.Organizations.Where(o => o.ArchivedAt == null)
            .Select(o => new { o.Id, o.ClientNumber, o.Name, o.Status })
            .ToListAsync(ct);

        var visitCounts = await _db.Appointments
            .Join(_db.Patients, a => a.PatientId, p => p.Id, (a, p) => new { a.Status, a.StartsAt, p.OrganizationId })
            .Where(x => x.Status == AppointmentStatus.Completed && x.StartsAt >= fromInstant && x.StartsAt < toInstant)
            .GroupBy(x => x.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.OrganizationId, g => g.Count, ct);

        var revenueByOrg = await _db.Charges
            .Where(c => c.Status != ChargeStatus.Void && c.ServiceDate >= from && c.ServiceDate <= to)
            .GroupBy(c => c.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, Total = g.Sum(c => c.ChargeAmount) })
            .ToDictionaryAsync(g => g.OrganizationId, g => g.Total, ct);

        var newPatientsByOrg = await _db.Patients
            .Where(p => p.DeletedAt == null && p.CreatedAt >= fromInstant && p.CreatedAt < toInstant)
            .GroupBy(p => p.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.OrganizationId, g => g.Count, ct);

        var userCounts = await _db.Users.Where(u => u.OrganizationId != null)
            .GroupBy(u => u.OrganizationId!.Value)
            .Select(g => new { OrganizationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.OrganizationId, g => g.Count, ct);

        var locationCounts = await _db.Locations
            .GroupBy(l => l.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.OrganizationId, g => g.Count, ct);

        var rows = organizations.Select(o => new OrganizationPerformanceRowDto(
            o.Id, o.ClientNumber, o.Name, o.Status,
            visitCounts.GetValueOrDefault(o.Id), revenueByOrg.GetValueOrDefault(o.Id),
            newPatientsByOrg.GetValueOrDefault(o.Id), userCounts.GetValueOrDefault(o.Id), locationCounts.GetValueOrDefault(o.Id)))
            .OrderByDescending(r => r.Revenue).ToList();

        return new OrganizationPerformanceDto(from, to, rows);
    }
}
