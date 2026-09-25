using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;

namespace PhysioTrac.Application.SuperAdmin;

/// <summary>Direct port of `care/client_management.py` — transactional
/// platform-client (tenant) provisioning and lifecycle. Every mutating
/// method here is platform-super-admin-only; callers must have already
/// checked <c>ITenantAccessService.RequirePlatformSuperAdmin</c>.</summary>
public interface IClientProvisioningService
{
    Task<ProvisionedClientResult> ProvisionClientAsync(ProvisionClientRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<ClientDto> UpdateClientAsync(long clientNumber, UpdateClientRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<ClientDto> SuspendClientAsync(long clientNumber, string reason, ICurrentUser actor, CancellationToken ct = default);

    Task<ClientDto> ActivateClientAsync(long clientNumber, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>A client's own subscription ending -- distinct from
    /// SuspendClientAsync (for-cause, reversible by support) even though
    /// both currently just flip Status and block login/access. See
    /// OrganizationStatus's own doc comment for why they're tracked
    /// separately.</summary>
    Task<ClientDto> CancelClientAsync(long clientNumber, string reason, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Soft-delete. Data is never removed; access is blocked like suspension.</summary>
    Task<ClientDto> ArchiveClientAsync(long clientNumber, string? reason, ICurrentUser actor, CancellationToken ct = default);

    Task<ClientDto?> GetClientAsync(long clientNumber, CancellationToken ct = default);

    Task<PagedResult<ClientDto>> ListClientsAsync(ClientListQuery query, CancellationToken ct = default);

    /// <summary>Issues a fresh invitation for a client's administrator,
    /// invalidating older unused ones first. Returns the raw token (only the
    /// hash is persisted) and the activation URL — since real email delivery
    /// isn't wired up yet, the caller surfaces these directly, matching the
    /// original's own `developmentInviteToken` escape hatch.</summary>
    Task<(string Token, string ActivationUrl)> ResendAdminInvitationAsync(long clientNumber, CancellationToken ct = default);
}
