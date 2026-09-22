using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.SuperAdmin;

/// <summary>Direct port of `care/privileged_access.py` — the break-glass
/// workflow: time-boxed, reasoned, audited clinical access for platform
/// super administrators, who otherwise have zero standing clinical access
/// (enforced in <c>ITenantAccessService.OrganizationRequiredAsync</c>).</summary>
public interface IPrivilegedAccessService
{
    public static readonly int[] AllowedDurationsHours = { 1, 4, 24 };

    Task<PrivilegedAccessGrant?> ActiveGrantAsync(Guid organizationId, Guid actorId, CancellationToken ct = default);

    Task<PrivilegedAccessGrant> RequestAsync(Guid organizationId, ICurrentUser actor, string reason, int durationHours, CancellationToken ct = default);

    Task<PrivilegedAccessGrant> RevokeAsync(Guid grantId, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<PrivilegedAccessGrant>> ListAsync(Guid organizationId, CancellationToken ct = default);
}
