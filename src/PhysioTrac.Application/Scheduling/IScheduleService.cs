using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Scheduling;

/// <summary>Read-only queries behind the staff calendar (Day/Week/Month/
/// Year/List). Every query is scoped to the caller's organization, and to
/// just their own appointments for a Therapist/Assistant -- the same
/// narrowing IAppointmentService.ListForRangeAsync applies. Patient-role
/// accounts are refused outright; they use the portal. Mutations stay on
/// IAppointmentService.</summary>
public interface IScheduleService
{
    Task<ScheduleSettingsDto> GetSettingsAsync(ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Week/Month views: appointments overlapping [From, To) with
    /// display names already resolved, plus the provider list for filters.</summary>
    Task<ScheduleRangeDto> GetRangeAsync(ICurrentUser actor, ScheduleQuery query, CancellationToken ct = default);

    /// <summary>Day view: one column per provider with working hours,
    /// blocked time, and a workload summary, all in clinic-local time.</summary>
    Task<ScheduleDayDto> GetDayAsync(ICurrentUser actor, DateOnly date, Guid? locationId, Guid? providerId, CancellationToken ct = default);

    /// <summary>Year view: non-cancelled appointment counts per clinic-local day.</summary>
    Task<IReadOnlyList<ScheduleDayCountDto>> GetCountsAsync(
        ICurrentUser actor, DateOnly from, DateOnly to, Guid? locationId, Guid? providerId, CancellationToken ct = default);

    /// <summary>List view, paginated.</summary>
    Task<PagedScheduleAppointmentsDto> GetListAsync(ICurrentUser actor, ScheduleQuery query, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Patient picker for booking: matches name, MRN, phone, or
    /// date of birth, within the caller's own organization only.</summary>
    Task<IReadOnlyList<SchedulePatientDto>> SearchPatientsAsync(ICurrentUser actor, string term, CancellationToken ct = default);
}

public record ScheduleQuery(
    DateTimeOffset From, DateTimeOffset To,
    Guid? ProviderId = null, Guid? LocationId = null, AppointmentStatus? Status = null,
    Guid? AppointmentTypeId = null, string? PatientSearch = null);

public record ScheduleSettingsDto(
    string Timezone, int SlotMinutes, bool CanCreate, bool CanReschedule, bool CanOverride, bool AllowDoubleBookOverride, bool CanManageAvailability,
    IReadOnlyList<ScheduleLocationDto> Locations, IReadOnlyList<ScheduleProviderDto> Providers,
    IReadOnlyList<ScheduleAppointmentTypeDto> AppointmentTypes);

public record ScheduleLocationDto(Guid Id, string Name, string Timezone, string? State);

/// <summary>UserId is the clinician's login -- the TherapistId a booking
/// for this provider must carry. Null means they can't be booked.</summary>
public record ScheduleProviderDto(
    Guid Id, string Name, string? Credentials, ProviderDiscipline Discipline, bool IsActive, bool HasLogin,
    IReadOnlyList<Guid> LocationIds, Guid? UserId);

public record ScheduleAppointmentTypeDto(Guid Id, string Name, int DefaultDurationMinutes, string? Color, AppointmentKind? DefaultKind);

public record ScheduleAppointmentDto(
    Guid Id, string ConfirmationNumber,
    Guid PatientId, string PatientName, string MedicalRecordNumber, DateOnly PatientDateOfBirth, string? PatientPhone,
    Guid TherapistId, Guid? ProviderId, string? ProviderName, string? ProviderCredentials, ProviderDiscipline? ProviderDiscipline,
    Guid? LocationId, string? LocationName, Guid? AppointmentTypeId, string? AppointmentTypeName, string? AppointmentTypeColor,
    AppointmentKind Kind, AppointmentStatus Status, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int DurationMinutes,
    bool IsHomeVisit, string? ReasonForVisit, Guid? SeriesId);

public record ScheduleRangeDto(string Timezone, IReadOnlyList<ScheduleAppointmentDto> Appointments);

public record TimeWindowDto(string Start, string End, Guid? LocationId, string? LocationName);

/// <summary>Kind is "TimeOff" (Reason = Lunch/Vacation/...) or "Closure".</summary>
public record ScheduleBlockDto(DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Kind, string Reason);

public record ProviderDaySummaryDto(
    int Appointments, int Scheduled, int CheckedIn, int InProgress, int Completed, int Cancelled, int NoShow, int Remaining,
    int WorkingMinutes, int BookedMinutes, int BlockedMinutes, int AvailableMinutes, int? UtilizationPercent,
    DateTimeOffset? FirstAppointmentAt, DateTimeOffset? LastAppointmentEndsAt,
    Guid? CurrentAppointmentId, Guid? NextAppointmentId);

public record ProviderDayColumnDto(
    ScheduleProviderDto Provider, IReadOnlyList<TimeWindowDto> WorkingHours, IReadOnlyList<ScheduleBlockDto> Blocks,
    ProviderDaySummaryDto Summary);

public record ScheduleDayDto(
    DateOnly Date, string Timezone, int SlotMinutes,
    IReadOnlyList<ProviderDayColumnDto> Providers, IReadOnlyList<ScheduleAppointmentDto> Appointments);

public record ScheduleDayCountDto(DateOnly Date, int Count);

public record PagedScheduleAppointmentsDto(IReadOnlyList<ScheduleAppointmentDto> Items, int Total, int Page, int PageSize);

public record SchedulePatientDto(
    Guid Id, string FullName, string MedicalRecordNumber, DateOnly DateOfBirth, string? Phone, string? Email, Guid? PrimaryLocationId);
