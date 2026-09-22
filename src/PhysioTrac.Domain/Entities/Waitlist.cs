using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>A patient's request to be seen sooner than their next confirmed
/// slot — joined from the portal, worked from the staff schedule when an
/// opening appears. Preferences are optional filters; blank means "any."</summary>
public class Waitlist : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }

    public Guid? AppointmentTypeId { get; set; }
    public AppointmentType? AppointmentType { get; set; }

    public Guid? ProviderId { get; set; }
    public Provider? Provider { get; set; }

    public DateOnly EarliestDate { get; set; }
    public DateOnly? LatestDate { get; set; }
    public string? Notes { get; set; }
    public WaitlistStatus Status { get; set; } = WaitlistStatus.Active;
}
