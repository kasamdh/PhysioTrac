using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>Which appointment types a provider is eligible to perform — a
/// provider with no row here for a given type is never offered it during booking.</summary>
public class ProviderAppointmentType : BaseEntity
{
    public Guid ProviderId { get; set; }
    public Provider? Provider { get; set; }

    public Guid AppointmentTypeId { get; set; }
    public AppointmentType? AppointmentType { get; set; }

    public bool Active { get; set; } = true;
    public int? CustomDurationMinutes { get; set; }
    public decimal? CustomPrice { get; set; }
}
