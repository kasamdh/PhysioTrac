namespace PhysioTrac.Application.Booking;

/// <summary>Direct port of `care/api/public_booking.py` + the public half of
/// `care/booking.py`. Every method resolves the organization strictly from
/// the URL slug — never from a client-supplied id — and every downstream
/// lookup is filtered by that resolved organization. Nothing returned here
/// leaks internal ids beyond what's needed to complete a booking, or private
/// administrative data.
///
/// Rate limiting is IP-based, in-process (<see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/>)
/// — the same documented, dev-only limitation as the original's default
/// Django cache backend; a shared backend (Redis) is required for correctness
/// under multiple worker processes in production.</summary>
public interface IPublicBookingService
{
    /// <summary>Null if the organization doesn't exist, is archived/suspended,
    /// or hasn't opted into online booking.</summary>
    Task<PublicOrganizationDto?> GetOrganizationAsync(string slug, CancellationToken ct = default);

    Task<IReadOnlyList<PublicLocationDto>> ListLocationsAsync(string slug, CancellationToken ct = default);

    Task<IReadOnlyList<PublicAppointmentTypeDto>> ListAppointmentTypesAsync(string slug, Guid? locationId = null, CancellationToken ct = default);

    Task<IReadOnlyList<PublicProviderDto>> ListProvidersAsync(string slug, Guid locationId, Guid appointmentTypeId, CancellationToken ct = default);

    /// <summary>Throws <see cref="RateLimitedException"/> if the caller's IP
    /// has exceeded the availability-lookup rate limit.</summary>
    Task<PublicAvailabilityDto> GetAvailabilityAsync(
        string slug, Guid locationId, Guid appointmentTypeId, DateOnly date, Guid? providerId,
        string? callerIp, CancellationToken ct = default);

    /// <summary>Transactional, fully re-validating create — the slot search
    /// above is a preview only. Locks the provider row for the duration of
    /// the transaction to close the double-booking race, then re-checks the
    /// slot is still open. Throws <see cref="BookingException"/> and
    /// subtypes on any failure; throws <see cref="RateLimitedException"/> if
    /// the caller's IP has exceeded the booking-creation rate limit.</summary>
    Task<PublicBookingResult> CreateBookingAsync(PublicBookingRequest request, string? callerIp, CancellationToken ct = default);
}
