using PhysioTrac.Application.Auth;

namespace PhysioTrac.Application.Dashboards;

/// <summary>Org-scoped operational/clinical/financial dashboards -- every
/// method is tenant-isolated via ITenantAccessService.OrganizationRequiredAsync
/// and role-gated to a staff role (never Patient); a Therapist/Assistant
/// additionally sees only their own caseload/visits, never the whole
/// organization's, the same narrowing IAppointmentService.ListForRangeAsync
/// already applies. "Today's schedule" itself needs no new method here --
/// it's exactly IAppointmentService.ListForRangeAsync for [today, tomorrow),
/// already exposed at GET /api/v1/appointments.</summary>
public interface IDashboardService
{
    Task<NewPatientsDto> GetNewPatientsAsync(ICurrentUser actor, DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<CancellationsNoShowsDto> GetCancellationsAndNoShowsAsync(
        ICurrentUser actor, DateOnly from, DateOnly to, Guid? providerId = null, Guid? locationId = null, CancellationToken ct = default);

    Task<ProviderProductivityDto> GetProviderProductivityAsync(ICurrentUser actor, DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<VisitsAndRetentionDto> GetVisitsAndRetentionAsync(ICurrentUser actor, DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<ReferralSourcesDto> GetReferralSourcesAsync(ICurrentUser actor, DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<LocationPerformanceDto> GetLocationPerformanceAsync(ICurrentUser actor, DateOnly from, DateOnly to, CancellationToken ct = default);
}
