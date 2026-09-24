namespace PhysioTrac.Domain.Enums;

public enum OrganizationStatus
{
    Active,
    Suspended
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
