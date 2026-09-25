namespace PhysioTrac.Domain.Enums;

/// <summary>Trial is the default status a freshly provisioned organization
/// starts in (see Organization.TrialEndDate) -- Active means a real,
/// paying subscription; Suspended is temporary/for-cause (billing failure,
/// policy violation), reversible via ActivateClientAsync; Cancelled is a
/// subscription the client themselves ended, tracked separately from
/// Suspended so support staff can tell "we cut them off" from "they left"
/// at a glance. Both Suspended and Cancelled organizations are blocked from
/// logging in (see AuthController.Login) and from every tenant-scoped
/// action (ITenantAccessService.OrganizationRequiredAsync).</summary>
public enum OrganizationStatus
{
    Trial,
    Active,
    Suspended,
    Cancelled,
}

public enum SubscriptionTier
{
    Starter,
    Professional,
    Premium,
    Enterprise
}

public enum UserRole
{
    SuperAdmin,
    Admin,
    Director,
    Therapist,
    Assistant,
    Scheduler,
    Biller,
    Compliance,
    Patient
}

public enum UserStatus
{
    Active,
    Inactive,
    LockedOut,
    Suspended,
    Deleted
}

public enum PatientStatus
{
    Active,
    Inactive,
    Discharged
}

public enum ContactMethod
{
    Email,
    Phone,
    Sms
}

public enum SessionRevokedReason
{
    None,
    NewLogin,
    UserLogout,
    IdleTimeout,
    AbsoluteTimeout,
    PasswordChanged,
    PasswordReset,
    AccountLocked,
    AccountSuspended,
    AccountDeactivated,
    AccountDeleted,
    AdminRevoked
}

public enum AllergySeverity
{
    Unknown,
    Mild,
    Moderate,
    Severe,
    LifeThreatening
}
