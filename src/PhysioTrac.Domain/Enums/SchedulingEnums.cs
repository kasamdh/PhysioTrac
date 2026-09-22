namespace PhysioTrac.Domain.Enums;

/// <summary>Matches Django's `ProviderAvailability.Weekday` numbering
/// (Monday=0..Sunday=6), not .NET's built-in `DayOfWeek` (Sunday=0) — use
/// <see cref="PhysioTrac.Domain.Scheduling.WeekdayExtensions"/> to convert.</summary>
public enum Weekday
{
    Monday = 0,
    Tuesday = 1,
    Wednesday = 2,
    Thursday = 3,
    Friday = 4,
    Saturday = 5,
    Sunday = 6,
}

public enum AppointmentKind
{
    Evaluation,
    FollowUp,
    Progress,
    Discharge,
    Telehealth,
}

public enum AppointmentStatus
{
    Scheduled,
    CheckedIn,
    Completed,
    Cancelled,
    NoShow,
}

public enum BookingSource
{
    FrontDesk,
    PatientPortal,
    PublicBooking,
    Provider,
    MobileApp,
}

public enum TimeOffReason
{
    Vacation,
    Personal,
    Conference,
    Lunch,
    Meeting,
    Admin,
    Other,
}

public enum TimeOffStatus
{
    Approved,
    Cancelled,
}
