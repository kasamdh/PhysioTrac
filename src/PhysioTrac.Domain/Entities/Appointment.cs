using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>A scheduled clinic, telehealth, or home-visit appointment.
///
/// Deliberately omits the original's `episode_of_care`/`authorization` FKs —
/// those entities aren't ported yet (a later module); this is a known,
/// documented gap, not an oversight.</summary>
public class Appointment : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>The assigned clinician's login identity. Stored by id only —
    /// Domain doesn't reference Infrastructure's ApplicationUser.</summary>
    public Guid TherapistId { get; set; }

    public Guid? ProviderId { get; set; }
    public Provider? Provider { get; set; }

    public AppointmentKind Kind { get; set; } = AppointmentKind.FollowUp;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }

    public string? Location { get; set; }
    public Guid? LocationDetailId { get; set; }
    public Domain.Entities.Location? LocationDetail { get; set; }

    public Guid? RoomId { get; set; }
    public Room? Room { get; set; }

    public bool IsHomeVisit { get; set; }
    public string? PrivateNotes { get; set; }

    public Guid? AppointmentTypeId { get; set; }
    public AppointmentType? AppointmentType { get; set; }

    public BookingSource BookingSource { get; set; } = BookingSource.FrontDesk;
    public string? ReasonForVisit { get; set; }

    public Guid CreatedById { get; set; }

    /// <summary>When the patient confirmed this visit via the portal. Null
    /// means unconfirmed.</summary>
    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>Set only for an occurrence generated as part of a recurring
    /// series -- null for a one-off appointment. "Edit one" means changing
    /// this row directly without touching SeriesId; "edit series" means
    /// AppointmentService.UpdateSeriesAsync, which touches every other
    /// still-future, still-Scheduled/Confirmed row sharing this id.</summary>
    public Guid? SeriesId { get; set; }
    public AppointmentSeries? Series { get; set; }

    public string ConfirmationNumber => $"APT-{Id.ToString("N")[..8].ToUpperInvariant()}";
}
