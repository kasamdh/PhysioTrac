using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>A block of time a provider is unavailable for booking.</summary>
public class ProviderTimeOff : BaseEntity
{
    public Guid ProviderId { get; set; }
    public Provider? Provider { get; set; }

    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }

    public DateTimeOffset StartDateTime { get; set; }
    public DateTimeOffset EndDateTime { get; set; }
    public TimeOffReason Reason { get; set; } = TimeOffReason.Other;
    public string? Notes { get; set; }
    public TimeOffStatus Status { get; set; } = TimeOffStatus.Approved;
}
