using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Scheduling;

/// <summary>Direct port of `care/availability.py` — authoritative slot
/// calculation. Nothing here is ever trusted from the caller; every call
/// recomputes from the provider's configured working hours minus existing
/// appointments, time off, and location closures.</summary>
public interface IAvailabilityService
{
    /// <summary>Providers who can be publicly booked for this appointment
    /// type at this location: active, online-booking-enabled, linked to a
    /// login identity, and explicitly offering this type.</summary>
    Task<IReadOnlyList<Provider>> EligibleProvidersAsync(Guid organizationId, Guid locationId, Guid appointmentTypeId, CancellationToken ct = default);

    Task<IReadOnlyList<Slot>> GetProviderSlotsAsync(
        Guid providerId, Guid locationId, Guid appointmentTypeId, DateOnly onDate,
        Guid? excludeAppointmentId = null, CancellationToken ct = default);

    /// <summary>Slots for one specific provider, or aggregated across every
    /// eligible provider when <paramref name="providerId"/> is null ("Any
    /// Available Therapist").</summary>
    Task<IReadOnlyList<ProviderSlotsDto>> GetAvailableSlotsAsync(
        Guid organizationId, Guid locationId, Guid appointmentTypeId, DateOnly onDate,
        Guid? providerId = null, Guid? excludeAppointmentId = null, CancellationToken ct = default);
}
