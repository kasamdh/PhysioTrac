using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Scheduling;

/// <summary>Staff-facing appointment creation/lifecycle — the authenticated
/// counterpart of `care/booking.py`'s transactional, re-validating create
/// path. The unauthenticated public-booking surface and patient-portal
/// self-service booking are deliberately out of scope here (a later module):
/// they need their own rate-limiting/CSRF-exempt surface design, not just
/// this service reused as-is.</summary>
public interface IAppointmentService
{
    /// <summary>Creates an appointment inside one transaction, locking the
    /// provider row (when a provider is set) to serialize concurrent booking
    /// attempts, then re-checking for provider/patient/room conflicts — the
    /// same two-layer defense (row lock + conflict re-check) as the original,
    /// extended to all three resources this phase asks for.</summary>
    Task<Appointment> CreateAsync(CreateAppointmentRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<Appointment> CancelAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default);

    Task<Appointment> ConfirmAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default);

    Task<Appointment> CheckInAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default);

    Task<Appointment> CompleteAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default);

    Task<Appointment> MarkNoShowAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>"Edit one" -- moves a single occurrence (drag-and-drop lands
    /// here) with the exact same conflict re-check CreateAsync runs, minus
    /// itself from the conflict query. Never changes Status.</summary>
    Task<Appointment> RescheduleAsync(Guid appointmentId, RescheduleAppointmentRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<IReadOnlyList<AppointmentStatusHistory>> GetStatusHistoryAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Generates every occurrence up front (not lazily) so each one
    /// gets its own real conflict check before the series is committed --
    /// all-or-nothing: if any occurrence conflicts, the whole series is
    /// rejected and none of it is created, with the conflicting dates named
    /// in the error so the caller can adjust and retry.</summary>
    Task<AppointmentSeries> CreateSeriesAsync(CreateAppointmentSeriesRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<AppointmentSeries> GetSeriesAsync(Guid seriesId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>"Edit series" -- cancels every still-future, still-Scheduled/
    /// Confirmed occurrence in the series (past and already-completed/
    /// cancelled/no-show occurrences are untouched). Returns the count
    /// actually cancelled.</summary>
    Task<int> CancelSeriesAsync(Guid seriesId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Appointments visible to this caller: the whole org for
    /// Admin/Director/Scheduler, just this caller's own for Therapist/
    /// Assistant — mirrors <c>ITenantAccessService.PatientsFor</c>'s caseload
    /// narrowing, applied to the schedule instead of the chart list.
    /// Optional providerId/locationDetailId filters back the calendar's own
    /// filter controls.</summary>
    Task<IReadOnlyList<Appointment>> ListForRangeAsync(
        ICurrentUser actor, DateTimeOffset from, DateTimeOffset to,
        Guid? providerId = null, Guid? locationDetailId = null, CancellationToken ct = default);
}
