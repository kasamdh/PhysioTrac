using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Configuration;
using PhysioTrac.Application.Sessions;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Application.Users;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

public class UserManagementService : IUserManagementService
{
    private readonly PhysioTracDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITenantAccessService _tenantAccess;
    private readonly ISessionService _sessions;
    private readonly IAuditService _audit;
    private readonly AppOptions _appOptions;

    public UserManagementService(
        PhysioTracDbContext db, UserManager<ApplicationUser> userManager, ITenantAccessService tenantAccess,
        ISessionService sessions, IAuditService audit, IOptions<AppOptions> appOptions)
    {
        _db = db;
        _userManager = userManager;
        _tenantAccess = tenantAccess;
        _sessions = sessions;
        _audit = audit;
        _appOptions = appOptions.Value;
    }

    public async Task<IReadOnlyList<StaffUserDto>> ListAsync(ICurrentUser actor, CancellationToken ct = default)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var users = await _db.Users
            .Where(u => u.OrganizationId == organization.Id)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync(ct);
        return users.Select(ToDto).ToList();
    }

    public async Task<(StaffUserDto User, string Token, string ActivationUrl)> InviteAsync(
        ICurrentUser actor, InviteUserRequest request, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        if (request.Role == UserRole.SuperAdmin)
        {
            throw new InvalidOperationException("SuperAdmin is a platform-level role and can't be assigned to an organization's staff.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _userManager.FindByNameAsync(email) is not null || await _userManager.FindByEmailAsync(email) is not null)
        {
            throw new InvalidOperationException("An account with that email already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            OrganizationId = organization.Id,
            Role = request.Role,
            Status = UserStatus.Inactive,
        };
        // Mirrors ClientProvisioningService's admin creation: no usable
        // password until the invitation is activated.
        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(e => e.Description)));
        }

        var (token, tokenHash) = InvitationTokenGenerator.Generate();
        _db.ClientInvitations.Add(new ClientInvitation
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
        });
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(
            actor.UserId, "user.invited", nameof(ApplicationUser), user.Id, organization.Id,
            metadata: new { role = request.Role.ToString() }, ct: ct);

        var activationUrl = $"{_appOptions.FrontendBaseUrl}/{organization.Slug}/activate?token={token}";
        return (ToDto(user), token, activationUrl);
    }

    public async Task<StaffUserDto> ChangeRoleAsync(ICurrentUser actor, Guid userId, UserRole newRole, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
        if (newRole == UserRole.SuperAdmin)
        {
            throw new InvalidOperationException("SuperAdmin is a platform-level role and can't be assigned to an organization's staff.");
        }

        var user = await LoadStaffUserInOrgAsync(actor, userId, ct);
        var previousRole = user.Role;
        user.Role = newRole;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(
            actor.UserId, "user.role_changed", nameof(ApplicationUser), user.Id, user.OrganizationId!.Value,
            metadata: new { from = previousRole.ToString(), to = newRole.ToString() }, ct: ct);

        return ToDto(user);
    }

    public async Task<StaffUserDto> DeactivateAsync(ICurrentUser actor, Guid userId, string? reason, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
        if (userId == actor.UserId)
        {
            throw new InvalidOperationException("You can't deactivate your own account.");
        }

        var user = await LoadStaffUserInOrgAsync(actor, userId, ct);
        user.Status = UserStatus.Suspended;
        user.SuspendedAt = DateTimeOffset.UtcNow;
        user.SuspendedById = actor.UserId;
        user.SuspensionReason = reason;
        await _db.SaveChangesAsync(ct);

        // Status alone wouldn't take effect until their next login --
        // SessionValidationMiddleware only re-checks session validity, not
        // account status, on each request. Revoking every active session
        // makes deactivation take effect immediately, on their very next
        // request.
        await _sessions.RevokeAllForUserAsync(user.Id, SessionRevokedReason.AccountSuspended, ct: ct);

        await _audit.RecordAuditEventAsync(
            actor.UserId, "user.deactivated", nameof(ApplicationUser), user.Id, user.OrganizationId!.Value,
            metadata: new { reason }, ct: ct);

        return ToDto(user);
    }

    public async Task<StaffUserDto> ActivateAsync(ICurrentUser actor, Guid userId, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
        var user = await LoadStaffUserInOrgAsync(actor, userId, ct);
        user.Status = UserStatus.Active;
        user.SuspendedAt = null;
        user.SuspendedById = null;
        user.SuspensionReason = null;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(
            actor.UserId, "user.activated", nameof(ApplicationUser), user.Id, user.OrganizationId!.Value, ct: ct);

        return ToDto(user);
    }

    private async Task<ApplicationUser> LoadStaffUserInOrgAsync(ICurrentUser actor, Guid userId, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User was not found.");
        if (user.OrganizationId != organization.Id)
        {
            throw new NotFoundException("User was not found.");
        }
        return user;
    }

    private static StaffUserDto ToDto(ApplicationUser u) => new(
        u.Id, u.UserName ?? string.Empty, u.Email, u.FirstName, u.LastName, u.Role, u.Status, u.MustChangePassword);
}
