using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Users;

/// <summary>Org-scoped staff account administration -- distinct from
/// IClientProvisioningService (platform-super-admin-only, manages the
/// organization's first admin at provisioning time). Every method here
/// resolves the acting user's own organization via OrganizationRequiredAsync
/// and operates only within it; there is no "specify an organization id"
/// parameter anywhere, by design.</summary>
public interface IUserManagementService
{
    Task<IReadOnlyList<StaffUserDto>> ListAsync(ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Creates the account with no usable password and issues a
    /// hashed, expiring invitation (the same scheme
    /// IClientProvisioningService uses) -- the caller surfaces the raw token/
    /// activation URL directly since real email delivery isn't wired up yet.</summary>
    Task<(StaffUserDto User, string Token, string ActivationUrl)> InviteAsync(
        ICurrentUser actor, InviteUserRequest request, CancellationToken ct = default);

    Task<StaffUserDto> ChangeRoleAsync(ICurrentUser actor, Guid userId, UserRole newRole, CancellationToken ct = default);

    Task<StaffUserDto> DeactivateAsync(ICurrentUser actor, Guid userId, string? reason, CancellationToken ct = default);

    Task<StaffUserDto> ActivateAsync(ICurrentUser actor, Guid userId, CancellationToken ct = default);
}

public record StaffUserDto(
    Guid Id, string UserName, string? Email, string FirstName, string LastName,
    UserRole Role, UserStatus Status, bool MustChangePassword);

public record InviteUserRequest(string FirstName, string LastName, string Email, UserRole Role);
