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

    public async Task<StaffUserDto> UpdateAsync(ICurrentUser actor, Guid userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.OrganizationAdministration);
        var user = await LoadStaffUserInOrgAsync(actor, userId, ct);
        var isSelf = user.Id == actor.UserId;
        var now = DateTimeOffset.UtcNow;

        var userName = request.UserName?.Trim() ?? string.Empty;
        var firstName = request.FirstName?.Trim() ?? string.Empty;
        var lastName = request.LastName?.Trim() ?? string.Empty;
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        var newPassword = string.IsNullOrEmpty(request.NewPassword) ? null : request.NewPassword;

        if (userName.Length == 0 || firstName.Length == 0 || lastName.Length == 0)
        {
            throw new InvalidOperationException("User ID, first name and last name are required.");
        }
        if (request.Role == UserRole.SuperAdmin)
        {
            throw new InvalidOperationException("SuperAdmin is a platform-level role and can't be assigned to an organization's staff.");
        }
        if (request.Status != user.Status && request.Status is not (UserStatus.Active or UserStatus.Suspended or UserStatus.Deleted))
        {
            throw new InvalidOperationException("Status can be set to Active, Suspended or Deleted.");
        }
        if (isSelf && (request.Role != user.Role || request.Status != user.Status))
        {
            throw new InvalidOperationException("You can't change your own status or access level.");
        }
        if (await _userManager.FindByNameAsync(userName) is { } sameName && sameName.Id != user.Id)
        {
            throw new InvalidOperationException("That user ID is already taken.");
        }
        if (email is not null && await _userManager.FindByEmailAsync(email) is { } sameEmail && sameEmail.Id != user.Id)
        {
            throw new InvalidOperationException("An account with that email already exists.");
        }

        var changed = new List<string>();
        if (user.UserName != userName) { user.UserName = userName; changed.Add("userName"); }
        if (user.FirstName != firstName) { user.FirstName = firstName; changed.Add("firstName"); }
        if (user.LastName != lastName) { user.LastName = lastName; changed.Add("lastName"); }
        if (user.Email != email) { user.Email = email; changed.Add("email"); }

        var previousRole = user.Role;
        if (user.Role != request.Role) { user.Role = request.Role; changed.Add("role"); }

        var previousStatus = user.Status;
        var signOutEverywhere = false;
        if (request.Status != user.Status)
        {
            changed.Add("status");
            user.Status = request.Status;
            user.StatusChangedAt = now;
            user.StatusChangedById = actor.UserId;
            switch (request.Status)
            {
                case UserStatus.Active:
                    user.SuspendedAt = null;
                    user.SuspendedById = null;
                    user.SuspensionReason = null;
                    user.ArchivedAt = null;
                    user.ArchivedById = null;
                    user.LockoutEnd = null;
                    user.AccessFailedCount = 0;
                    break;
                case UserStatus.Suspended:
                    user.SuspendedAt = now;
                    user.SuspendedById = actor.UserId;
                    signOutEverywhere = true;
                    break;
                case UserStatus.Deleted:
                    user.ArchivedAt = now;
                    user.ArchivedById = actor.UserId;
                    signOutEverywhere = true;
                    break;
            }
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        if (newPassword is not null)
        {
            if (await _userManager.HasPasswordAsync(user))
            {
                var removed = await _userManager.RemovePasswordAsync(user);
                if (!removed.Succeeded)
                {
                    throw new InvalidOperationException(string.Join(" ", removed.Errors.Select(e => e.Description)));
                }
            }
            var added = await _userManager.AddPasswordAsync(user, newPassword);
            if (!added.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", added.Errors.Select(e => e.Description)));
            }

            // Setting the password settles a pending invite: the account is
            // usable now, and the emailed link must not work any more.
            if (user.Status == UserStatus.Inactive)
            {
                user.Status = UserStatus.Active;
                user.StatusChangedAt = now;
                user.StatusChangedById = actor.UserId;
            }
            var openInvites = await _db.ClientInvitations.Where(i => i.UserId == user.Id && i.UsedAt == null).ToListAsync(ct);
            foreach (var invite in openInvites) invite.UsedAt = now;
            if (!isSelf) user.MustChangePassword = true;
            await _userManager.UpdateAsync(user);
            if (!isSelf) signOutEverywhere = true;

            // Never record the password itself.
            await _audit.RecordAuditEventAsync(
                actor.UserId, "user.password_reset", nameof(ApplicationUser), user.Id, user.OrganizationId!.Value, ct: ct);
        }

        if (signOutEverywhere)
        {
            var reason = user.Status == UserStatus.Active ? SessionRevokedReason.PasswordReset : SessionRevokedReason.AccountSuspended;
            await _sessions.RevokeAllForUserAsync(user.Id, reason, ct: ct);
        }

        if (changed.Count > 0)
        {
            await _audit.RecordAuditEventAsync(
                actor.UserId, "user.updated", nameof(ApplicationUser), user.Id, user.OrganizationId!.Value,
                metadata: new
                {
                    fields = changed,
                    role = changed.Contains("role") ? new { from = previousRole.ToString(), to = user.Role.ToString() } : null,
                    status = changed.Contains("status") ? new { from = previousStatus.ToString(), to = user.Status.ToString() } : null,
                },
                ct: ct);
        }

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
