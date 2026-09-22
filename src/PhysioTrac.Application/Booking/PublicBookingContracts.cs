using PhysioTrac.Application.Scheduling;

namespace PhysioTrac.Application.Booking;

public record PublicOrganizationDto(string Name, string Slug, string? LogoUrl, string? AddressLine1, string? AddressLine2, string? City, string? State, string? ZipCode, string? Phone, string Timezone);

public record PublicLocationDto(Guid Id, string Name, string? City, string? State, string Timezone);

public record PublicAppointmentTypeDto(Guid Id, string Name, string? Description, int DurationMinutes, decimal? Price, bool RequiresNewPatient);

public record PublicProviderDto(Guid Id, string DisplayName, string? Credentials, string? Specialty, string? Bio);

public record PublicAvailabilityDto(DateOnly Date, string Timezone, IReadOnlyList<ProviderSlotsDto> Providers);

public record PublicPatientInfo(string FirstName, string LastName, DateOnly DateOfBirth, string? Email, string? Phone, string? Address, string? EmergencyContact);

public record PublicBookingRequest(
    string OrganizationSlug, Guid LocationId, Guid AppointmentTypeId, Guid ProviderId,
    DateTimeOffset StartDatetime, bool IsNewPatient, PublicPatientInfo Patient, string? ReasonForVisit);

public record PublicBookingResult(
    string ConfirmationNumber, Guid AppointmentId, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    string? ProviderName, string? LocationName, string? AppointmentTypeName, string OrganizationName);
