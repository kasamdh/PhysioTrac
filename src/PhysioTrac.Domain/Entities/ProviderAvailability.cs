using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>One recurring weekly working-hours window for a provider at a
/// location. Multiple rows per (provider, location, day) are expected — that's
/// how a lunch break splits a day into two windows.</summary>
public class ProviderAvailability : BaseEntity
{
    public Guid ProviderId { get; set; }
    public Provider? Provider { get; set; }

    public Guid LocationId { get; set; }
    public Location? Location { get; set; }

    public Weekday DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool Active { get; set; } = true;
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveUntil { get; set; }
}
