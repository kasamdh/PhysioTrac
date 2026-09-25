using PhysioTrac.Application.Auth;

namespace PhysioTrac.Application.Dashboards;

/// <summary>Cross-tenant organization/location performance for the platform
/// Super Admin workspace -- the one legitimate place this app aggregates
/// across organizations. Requires ITenantAccessService.RequirePlatformSuperAdmin.</summary>
public interface IPlatformDashboardService
{
    Task<OrganizationPerformanceDto> GetOrganizationPerformanceAsync(ICurrentUser actor, DateOnly from, DateOnly to, CancellationToken ct = default);
}
