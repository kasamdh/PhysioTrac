using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>A clinic-wide closure at one location during which no provider
/// at that location can be booked.</summary>
public class LocationClosure : BaseEntity
{
    public Guid LocationId { get; set; }
    public Location? Location { get; set; }

    public DateTimeOffset StartDateTime { get; set; }
    public DateTimeOffset EndDateTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
}
