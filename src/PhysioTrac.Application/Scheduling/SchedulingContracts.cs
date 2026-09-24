using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Scheduling;

public record ProviderDto(
    Guid Id, string FirstName, string LastName, string FullName, string? Specialty, string? Credentials,
    string? NpiNumber, bool IsActive, bool OnlineBookingEnabled, IReadOnlyList<Guid> LocationIds);

public record CreateProviderRequest(string FirstName, string LastName, string? Specialty, string? Credentials, string? NpiNumber, Guid? UserId, IReadOnlyList<Guid>? LocationIds);

public record UpdateProviderRequest(string FirstName, string LastName, string? Specialty, string? Credentials, string? NpiNumber, bool IsActive, IReadOnlyList<Guid>? LocationIds);

public record AppointmentTypeDto(Guid Id, string Name, string? Description, int DefaultDurationMinutes, decimal? Price, bool IsActive, bool OnlineBookingEnabled);

public record CreateAppointmentTypeRequest(string Name, string? Description, int DefaultDurationMinutes, decimal? Price, bool OnlineBookingEnabled, bool RequiresNewPatient);

public record Slot(DateTimeOffset Start, DateTimeOffset End);

public record ProviderSlotsDto(Guid ProviderId, string ProviderName, IReadOnlyList<Slot> Slots);

public record AppointmentDto(
    Guid Id, Guid PatientId, Guid TherapistId, Guid? ProviderId,
    AppointmentKind Kind, AppointmentStatus Status,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    Guid? LocationDetailId, bool IsHomeVisit, string? ReasonForVisit,
    string ConfirmationNumber, DateTimeOffset? ConfirmedAt);

public record CreateAppointmentRequest(
    Guid PatientId, Guid TherapistId, Guid? ProviderId, Guid? LocationDetailId, Guid? AppointmentTypeId,
    AppointmentKind Kind, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    bool IsHomeVisit, string? ReasonForVisit);
