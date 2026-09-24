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
    ReEvaluation,
}

/// <summary>Blocked/unavailable time is deliberately NOT a value here --
/// blocking a provider's calendar is modeled as ProviderTimeOff, a separate
/// entity with no PatientId, rather than a fake appointment kind that would
/// need one anyway. The scheduling API/calendar surfaces both Appointments
/// and ProviderTimeOff rows side by side.</summary>
public enum AppointmentStatus
{
    Scheduled,
    Confirmed,
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
