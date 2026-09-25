using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Dashboards;

public record NewPatientsByDayDto(DateOnly Date, int Count);

public record NewPatientsDto(DateOnly From, DateOnly To, int TotalNewPatients, IReadOnlyList<NewPatientsByDayDto> ByDay);

public record CancellationsNoShowsDto(
    DateOnly From, DateOnly To, int TotalAppointments, int Completed, int Cancelled, int NoShow,
    decimal CancellationRate, decimal NoShowRate);

public record ProviderProductivityRowDto(Guid ProviderId, string ProviderName, int CompletedVisits, int TotalUnits, decimal TotalBilled);

public record ProviderProductivityDto(DateOnly From, DateOnly To, IReadOnlyList<ProviderProductivityRowDto> Rows);

/// <summary>Retention here means a specific, simple, documented
/// methodology, not a claimed industry-standard formula: split [From, To]
/// into two equal halves; EligiblePatients had >= 1 completed visit in the
/// first half; RetainedPatients is however many of those also had >= 1
/// completed visit in the second half. A wider or per-payer definition
/// (e.g. "seen again within 90 days of discharge") is a reporting choice a
/// real product would need to make configurable -- this is a starting
/// point, not the only valid one.</summary>
public record VisitsAndRetentionDto(
    DateOnly From, DateOnly To, int TotalVisits, int UniquePatientsSeen,
    int EligiblePatients, int RetainedPatients, decimal RetentionRate);

public record ReferralSourceRowDto(Guid? ReferringProviderId, string ReferringProviderName, int NewPatientCount);

public record ReferralSourcesDto(DateOnly From, DateOnly To, IReadOnlyList<ReferralSourceRowDto> Rows);

public record LocationPerformanceRowDto(Guid? LocationId, string LocationName, int Visits, decimal Revenue, int NewPatients);

public record LocationPerformanceDto(DateOnly From, DateOnly To, IReadOnlyList<LocationPerformanceRowDto> Rows);

/// <summary>Platform-Super-Admin-only, cross-tenant by design -- the one
/// legitimate place this app ever aggregates across organizations.</summary>
public record OrganizationPerformanceRowDto(
    Guid OrganizationId, long? ClientNumber, string OrganizationName, OrganizationStatus Status,
    int Visits, decimal Revenue, int NewPatients, int UserCount, int LocationCount);

public record OrganizationPerformanceDto(DateOnly From, DateOnly To, IReadOnlyList<OrganizationPerformanceRowDto> Rows);
