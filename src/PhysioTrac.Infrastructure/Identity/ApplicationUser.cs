using Microsoft.AspNetCore.Identity;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Infrastructure.Identity;

/// <summary>Application user with a single organization and least-privilege
/// role — the auth-identity counterpart of the original Django `User`
/// (`AbstractUser` subclass), which mixed auth fields and business/RBAC
/// fields on one row. We keep that same shape here rather than splitting
/// auth and business-user into two rows, since nothing in this system reads
/// one without the other.</summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public Guid? OrganizationId { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Therapist;
    public string? Credential { get; set; }
    public bool MustUseMfa { get; set; } = true;
    public bool MustChangePassword { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTimeOffset? StatusChangedAt { get; set; }
    public Guid? StatusChangedById { get; set; }

    public DateTimeOffset? SuspendedAt { get; set; }
    public Guid? SuspendedById { get; set; }
    public string? SuspensionReason { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }
    public Guid? ArchivedById { get; set; }

    /// <summary>Canonical cross-client platform account: superuser, role =
    /// SuperAdmin, and no standing organization. Has zero standing clinical
    /// access — enforced in <c>TenantAccessService</c>, not here.</summary>
    public bool IsPlatformSuperAdmin => Role == UserRole.SuperAdmin && OrganizationId is null;

    /// <summary>Status as it should be treated right now, healing an expired
    /// lockout for display purposes only — the underlying row is only
    /// written back to Active the next time this account actually attempts
    /// to authenticate. Lockout itself is tracked by ASP.NET Identity's own
    /// <see cref="IdentityUser{TKey}.LockoutEnd"/>/<c>AccessFailedCount</c>
    /// (via <c>UserManager</c>/<c>SignInManager</c>), not a duplicate field
    /// here.</summary>
    public UserStatus EffectiveStatus(DateTimeOffset now) =>
        Status == UserStatus.LockedOut && LockoutEnd is not null && LockoutEnd <= now
            ? UserStatus.Active
            : Status;

    public bool CanAccessClinical => Role is UserRole.Admin or UserRole.Director or UserRole.Therapist
        or UserRole.Assistant or UserRole.Compliance;

    public bool CanSignNotes => Role is UserRole.Admin or UserRole.Director or UserRole.Therapist;

    public bool CanManageSchedule => Role is UserRole.Admin or UserRole.Director or UserRole.Therapist
        or UserRole.Assistant or UserRole.Scheduler;
}
