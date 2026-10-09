using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Scheduling;

/// <param name="UserId">The staff login this provider signs in with (null = none).</param>
public record ProviderDto(
    Guid Id, string FirstName, string LastName, string FullName, string? Specialty, string? Credentials,
    string? NpiNumber, bool IsActive, bool OnlineBookingEnabled, IReadOnlyList<Guid> LocationIds,
    ProviderDiscipline Discipline, bool HasLogin, Guid? UserId = null);

public record CreateProviderRequest(string FirstName, string LastName, string? Specialty, string? Credentials, string? NpiNumber, Guid? UserId, IReadOnlyList<Guid>? LocationIds,
    ProviderDiscipline Discipline = ProviderDiscipline.Other, bool OnlineBookingEnabled = true);

/// <param name="Discipline">Null keeps the current discipline.</param>
/// <param name="OnlineBookingEnabled">Null keeps the current setting.</param>
/// <param name="UpdateLogin">When true, <paramref name="UserId"/> replaces the linked login (null unlinks it).</param>
public record UpdateProviderRequest(string FirstName, string LastName, string? Specialty, string? Credentials, string? NpiNumber, bool IsActive, IReadOnlyList<Guid>? LocationIds,
    ProviderDiscipline? Discipline = null, bool? OnlineBookingEnabled = null, bool UpdateLogin = false, Guid? UserId = null);

/// <summary>A staff login that can be linked to a provider.</summary>
public record LinkableUserDto(Guid Id, string Name, string UserName, string Role);

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
    bool IsHomeVisit, string? ReasonForVisit, string? OverrideReason = null, string? Notes = null);

public record RescheduleAppointmentRequest(
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, Guid? ProviderId, Guid? LocationDetailId, Guid? RoomId, string? OverrideReason = null);

public record CreateAppointmentSeriesRequest(
    Guid PatientId, Guid TherapistId, Guid? ProviderId, Guid? LocationDetailId, Guid? RoomId, Guid? AppointmentTypeId,
    AppointmentKind Kind, DateTimeOffset FirstStartsAt, DateTimeOffset FirstEndsAt,
    int IntervalWeeks, int OccurrenceCount, string? ReasonForVisit,
    string? Notes = null, IReadOnlyList<Weekday>? DaysOfWeek = null, DateOnly? EndDate = null, bool SkipConflicting = false);

/// <summary>One generated occurrence of a proposed series and every rule
/// it would break -- empty Violations means it can be booked.</summary>
public record SeriesOccurrencePreviewDto(DateTimeOffset StartsAt, DateTimeOffset EndsAt, IReadOnlyList<SchedulingViolation> Violations);

public record SeriesPreviewDto(int Bookable, int Conflicting, IReadOnlyList<SeriesOccurrencePreviewDto> Occurrences);

public record AppointmentSeriesDto(Guid Id, int IntervalWeeks, int OccurrenceCount, bool IsActive, IReadOnlyList<AppointmentDto> Occurrences);

public record AppointmentStatusHistoryDto(Guid Id, AppointmentStatus? FromStatus, AppointmentStatus ToStatus, Guid ChangedById, string? Reason, DateTimeOffset CreatedAt);
