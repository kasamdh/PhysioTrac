using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>The recurrence pattern behind a set of Appointment rows sharing
/// SeriesId -- deliberately NOT an rrule-style generic recurrence engine,
/// just the one pattern PT scheduling actually needs: the same weekday,
/// every N weeks, for a fixed number of visits. All occurrences are
/// generated up front at creation time (see AppointmentService), not
/// materialized lazily -- simpler to reason about, and each one gets its
/// own real conflict check before the series is committed.</summary>
public class AppointmentSeries : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid TherapistId { get; set; }
    public Guid? ProviderId { get; set; }
    public Guid? LocationDetailId { get; set; }
    public Guid? RoomId { get; set; }
    public Guid? AppointmentTypeId { get; set; }
    public AppointmentKind Kind { get; set; } = AppointmentKind.FollowUp;

    /// <summary>1 = every week, 2 = every other week, etc.</summary>
    public int IntervalWeeks { get; set; } = 1;
    public int OccurrenceCount { get; set; }

    public Guid CreatedById { get; set; }

    /// <summary>False once the series has been edited to cancel its
    /// remaining occurrences -- past/individually-modified occurrences are
    /// untouched either way, since editing "the series" only ever means
    /// its still-Scheduled/Confirmed future rows (see
    /// AppointmentService.UpdateSeriesAsync).</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<Appointment> Occurrences { get; set; } = new List<Appointment>();
}
