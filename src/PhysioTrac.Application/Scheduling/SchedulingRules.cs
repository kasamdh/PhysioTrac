namespace PhysioTrac.Application.Scheduling;

/// <summary>Stable codes for every reason a slot can't be booked, so the
/// calendar can react to a specific rule (e.g. offer "Choose another time"
/// for DOUBLE_BOOKED) rather than parsing a message.</summary>
public static class SchedulingViolationCodes
{
    public const string ProviderInactive = "PROVIDER_INACTIVE";
    public const string ProviderNotAtLocation = "PROVIDER_NOT_AT_LOCATION";
    public const string LicenseInvalid = "LICENSE_INVALID";
    public const string AppointmentTypeNotAllowed = "APPOINTMENT_TYPE_NOT_ALLOWED";
    public const string PtaScope = "PTA_SCOPE";
    public const string OutsideWorkingHours = "OUTSIDE_WORKING_HOURS";
    public const string TimeOff = "TIME_OFF";
    public const string LocationClosed = "LOCATION_CLOSED";
    public const string DoubleBooked = "DOUBLE_BOOKED";
    public const string PatientConflict = "PATIENT_CONFLICT";
    public const string RoomConflict = "ROOM_CONFLICT";
}

/// <summary>One broken scheduling rule. Overridable means an authorized
/// role may still book it with a recorded reason (availability and, when
/// the organization allows it, double-booking) -- never true for rules
/// about whether the provider may legally see this patient at all
/// (inactive, license, scope of practice, location, type).</summary>
public record SchedulingViolation(string Code, string Message, bool Overridable);

/// <summary>Thrown instead of a plain InvalidOperationException when a slot
/// breaks one or more scheduling rules. Still an InvalidOperationException,
/// so existing callers that only catch that keep working.</summary>
public class SchedulingConflictException : InvalidOperationException
{
    public IReadOnlyList<SchedulingViolation> Violations { get; }

    /// <summary>Whether *this caller* could book the slot anyway by
    /// resending the request with an override reason.</summary>
    public bool CanOverride { get; }

    public SchedulingConflictException(IReadOnlyList<SchedulingViolation> violations, bool canOverride)
        : base(string.Join(" ", violations.Select(v => v.Message)))
    {
        Violations = violations;
        CanOverride = canOverride;
    }
}

public record AppointmentSlotSummaryDto(
    Guid? ProviderId, string? ProviderName, Guid? LocationId, string? LocationName,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt);

/// <summary>Dry-run result for a reschedule/drag-and-drop move: what would
/// change, and every rule it would break. Nothing is saved.</summary>
public record MoveCheckDto(
    Guid AppointmentId, string PatientName,
    AppointmentSlotSummaryDto From, AppointmentSlotSummaryDto To,
    bool IsValid, bool CanOverride, IReadOnlyList<SchedulingViolation> Violations);
