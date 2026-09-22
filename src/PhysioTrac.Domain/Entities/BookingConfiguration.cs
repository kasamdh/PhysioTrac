using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>Per-organization public/portal booking policy. Online booking is
/// off by default — an organization must explicitly opt in.</summary>
public class BookingConfiguration : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public bool OnlineBookingEnabled { get; set; }
    public bool AllowNewPatients { get; set; } = true;
    public bool AllowReturningPatients { get; set; } = true;
    public bool AllowAnyAvailableTherapist { get; set; } = true;
    public int MinNoticeHours { get; set; } = 4;
    public int MaxAdvanceDays { get; set; } = 90;
    public int SlotIntervalMinutes { get; set; } = 15;
    public string? CancellationPolicy { get; set; }

    /// <summary>How many hours before an appointment a patient may still
    /// cancel or reschedule it online via the portal.</summary>
    public int PatientChangeCutoffHours { get; set; } = 24;
}
