using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Clinical;

/// <summary>Documentation dashboard filters. From/To bound the visits and
/// notes by date of service; without From, unsigned notes of any age are
/// listed (visits not started look back 30 days, recently signed 7 days).</summary>
public record DashboardFilter(
    Guid? ProviderId = null, Guid? PatientId = null, NoteType? NoteType = null, DocumentationStatus? Status = null,
    DateOnly? From = null, DateOnly? To = null);

/// <summary>A visit or a note on the dashboard.</summary>
public record DashboardItemDto(
    Guid PatientId, string PatientName, string MedicalRecordNumber, DateOnly Date,
    Guid? NoteId, NoteType? NoteType, NoteStatus? NoteStatus, DocumentationStatus? DocumentationStatus,
    Guid? AppointmentId, DateTimeOffset? StartsAt, AppointmentStatus? AppointmentStatus,
    string? AuthorName, string? ProviderName, int DaysSinceService, bool Overdue,
    DateTimeOffset? SignedAt = null, string? Detail = null);

/// <summary>A patient-level deadline: a progress note, re-evaluation or plan-of-care recertification.</summary>
public record DashboardDeadlineDto(
    Guid PatientId, string PatientName, string MedicalRecordNumber, NoteType NoteType, DateOnly? DueDate,
    bool Overdue, string Detail, Guid? PlanOfCareId = null);

public record DashboardCountsDto(
    int TodaysVisits, int NotStarted, int Drafts, int ReadyToSign, int AwaitingCosign, int Returned, int Overdue,
    int ProgressNotesDue, int ReevaluationsDue, int ExpiringPlans, int RecentlySigned);

public record DocumentationDashboardDto(
    DateOnly Today, DateOnly? From, DateOnly To,
    DashboardCountsDto Counts,
    IReadOnlyList<DashboardItemDto> TodaysSchedule,
    IReadOnlyList<DashboardItemDto> NotStarted,
    IReadOnlyList<DashboardItemDto> Drafts,
    IReadOnlyList<DashboardItemDto> ReadyToSign,
    IReadOnlyList<DashboardItemDto> AwaitingCosign,
    IReadOnlyList<DashboardItemDto> Returned,
    IReadOnlyList<DashboardItemDto> Overdue,
    IReadOnlyList<DashboardDeadlineDto> ProgressNotesDue,
    IReadOnlyList<DashboardDeadlineDto> ReevaluationsDue,
    IReadOnlyList<DashboardDeadlineDto> ExpiringPlans,
    IReadOnlyList<DashboardItemDto> RecentlySigned);

public static class DashboardRules
{
    /// <summary>A note is overdue when it isn't signed by the end of the day after the visit.</summary>
    public const int NoteDueDays = 1;

    /// <summary>Progress note at the 10th visit when the organization sets no visit count (Medicare's rule).</summary>
    public const int DefaultProgressVisitCount = 10;

    /// <summary>"Due soon" this many visits before the progress-note threshold.</summary>
    public const int ProgressDueSoonVisits = 2;

    public const int ReevaluationWindowDays = 7;
    public const int ExpiringPlanWindowDays = 30;

    public static bool IsOverdue(DateOnly serviceDate, DateOnly today) => serviceDate.AddDays(NoteDueDays) < today;
}
