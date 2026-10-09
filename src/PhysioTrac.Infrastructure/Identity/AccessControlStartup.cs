using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Identity;

/// <summary>Startup wiring for <see cref="AccessControl"/> that needs the
/// database: picks the organization a platform SuperAdmin works inside while
/// access control is off.</summary>
public static class AccessControlStartup
{
    public static async Task ResolveSuperAdminOrganizationAsync(IServiceProvider services, CancellationToken ct = default)
    {
        if (AccessControl.Enabled) return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhysioTracDbContext>();
        AccessControl.SuperAdminOrganizationId = await db.Organizations
            .Where(o => o.IsActive && o.ArchivedAt == null && o.Status != OrganizationStatus.Suspended)
            .OrderBy(o => o.ClientNumber)
            .Select(o => (Guid?)o.Id)
            .FirstOrDefaultAsync(ct);
    }
}
