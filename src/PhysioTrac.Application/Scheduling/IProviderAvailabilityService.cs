using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Scheduling;

/// <summary>A provider's weekly working hours and time off -- the data the
/// Day view shades and SchedulingRulesValidator enforces. Viewing is open to
/// all staff; changing it is <see cref="Tenancy.RoleSets.AvailabilityManagement"/>.
/// Every change is audited explicitly (neither entity carries its own
/// OrganizationId, so the automatic entity-change audit doesn't see them).</summary>
public interface IProviderAvailabilityService
{
    Task<ProviderScheduleDto> GetAsync(Guid providerId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Replaces the provider's whole weekly pattern in one step, so
    /// the editor can't leave it half-saved.</summary>
    Task<ProviderScheduleDto> ReplaceWeeklyHoursAsync(Guid providerId, ReplaceWeeklyHoursRequest request, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Adds time off -- once, or repeated on chosen weekdays until a
    /// date (e.g. a daily lunch block). Doesn't move existing appointments;
    /// it reports the ones that now fall inside the new time off so staff
    /// can reschedule them.</summary>
    Task<CreateTimeOffResultDto> CreateTimeOffAsync(Guid providerId, CreateTimeOffRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task CancelTimeOffAsync(Guid providerId, Guid timeOffId, ICurrentUser actor, CancellationToken ct = default);
}

/// <summary>Start/End are clinic-local "HH:mm".</summary>
public record WeeklyHoursWindowDto(
    Guid Id, Weekday DayOfWeek, Guid LocationId, string LocationName, string Start, string End,
    DateOnly? EffectiveFrom, DateOnly? EffectiveUntil);

public record WeeklyHoursWindowRequest(
    Weekday DayOfWeek, Guid LocationId, string Start, string End, DateOnly? EffectiveFrom = null, DateOnly? EffectiveUntil = null);

public record ReplaceWeeklyHoursRequest(IReadOnlyList<WeeklyHoursWindowRequest> Windows);

public record TimeOffDto(
    Guid Id, DateTimeOffset StartsAt, DateTimeOffset EndsAt, TimeOffReason Reason, string? Notes, Guid? LocationId, TimeOffStatus Status);

/// <summary>RepeatUntil/RepeatDays turn one block into one per matching day
/// (same clinic-local times) through RepeatUntil inclusive; RepeatDays
/// defaults to the first block's weekday.</summary>
public record CreateTimeOffRequest(
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, TimeOffReason Reason, string? Notes,
    DateOnly? RepeatUntil = null, IReadOnlyList<Weekday>? RepeatDays = null);

public record AffectedAppointmentDto(Guid Id, string PatientName, DateTimeOffset StartsAt, DateTimeOffset EndsAt);

public record CreateTimeOffResultDto(IReadOnlyList<TimeOffDto> Created, IReadOnlyList<AffectedAppointmentDto> AffectedAppointments);

public record ProviderScheduleDto(
    Guid ProviderId, string ProviderName, string? Credentials, string Timezone, bool CanManage,
    IReadOnlyList<ScheduleLocationDto> Locations,
    IReadOnlyList<WeeklyHoursWindowDto> WeeklyHours,
    IReadOnlyList<TimeOffDto> TimeOff);
