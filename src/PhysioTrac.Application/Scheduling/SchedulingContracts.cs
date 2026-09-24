using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Scheduling;

public record ProviderDto(
    Guid Id, string FirstName, string LastName, string FullName, string? Specialty, string? Credentials,
    string? NpiNumber, bool IsActive, bool OnlineBookingEnabled, IReadOnlyList<Guid> LocationIds);

public record CreateProviderRequest(string FirstName, string LastName, string? Specialty, string? Credentials, string? NpiNumber, Guid? UserId, IReadOnlyList<Guid>? LocationIds);

public record UpdateProviderRequest(string FirstName, string LastName, string? Specialty, string? Credentials, string? NpiNumber, bool IsActive, IReadOnlyList<Guid>? LocationIds);

public record AppointmentTypeDto(
    Guid Id, string Name, string? Description, int DefaultDurationMinutes, decimal? Price,
    bool IsActive, bool OnlineBookingEnabled, AppointmentKind? DefaultKind, string? DefaultCptCode);

public record CreateAppointmentTypeRequest(
    string Name, string? Description, int DefaultDurationMinutes, decimal? Price,
    bool OnlineBookingEnabled, bool RequiresNewPatient, AppointmentKind? DefaultKind, string? DefaultCptCode);

public record Slot(DateTimeOffset Start, DateTimeOffset End);

public record ProviderSlotsDto(Guid ProviderId, string ProviderName, IReadOnlyList<Slot> Slots);

public record AppointmentDto(
    Guid Id, Guid PatientId, Guid TherapistId, Guid? ProviderId,
    AppointmentKind Kind, AppointmentStatus Status,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    Guid? LocationDetailId, Guid? RoomId, bool IsHomeVisit, string? ReasonForVisit,
    string ConfirmationNumber, DateTimeOffset? ConfirmedAt, Guid? SeriesId);

public record CreateAppointmentRequest(
    Guid PatientId, Guid TherapistId, Guid? ProviderId, Guid? LocationDetailId, Guid? RoomId, Guid? AppointmentTypeId,
    AppointmentKind Kind, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    bool IsHomeVisit, string? ReasonForVisit);

public record RescheduleAppointmentRequest(DateTimeOffset StartsAt, DateTimeOffset EndsAt, Guid? ProviderId, Guid? LocationDetailId, Guid? RoomId);

public record CreateAppointmentSeriesRequest(
    Guid PatientId, Guid TherapistId, Guid? ProviderId, Guid? LocationDetailId, Guid? RoomId, Guid? AppointmentTypeId,
    AppointmentKind Kind, DateTimeOffset FirstStartsAt, DateTimeOffset FirstEndsAt,
    int IntervalWeeks, int OccurrenceCount, string? ReasonForVisit);

public record AppointmentSeriesDto(Guid Id, int IntervalWeeks, int OccurrenceCount, bool IsActive, IReadOnlyList<AppointmentDto> Occurrences);

public record AppointmentStatusHistoryDto(Guid Id, AppointmentStatus? FromStatus, AppointmentStatus ToStatus, Guid ChangedById, string? Reason, DateTimeOffset CreatedAt);
