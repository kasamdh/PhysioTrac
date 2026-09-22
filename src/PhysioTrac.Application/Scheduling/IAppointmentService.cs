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
    /// attempts, then re-checking for a therapist-schedule conflict — the
    /// same two-layer defense (row lock + conflict re-check) as the original.</summary>
    Task<Appointment> CreateAsync(CreateAppointmentRequest request, ICurrentUser actor, CancellationToken ct = default);

    Task<Appointment> CancelAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default);

    /// <summary>Appointments visible to this caller: the whole org for
    /// Admin/Director/Scheduler, just this caller's own for Therapist/
    /// Assistant — mirrors <c>ITenantAccessService.PatientsFor</c>'s caseload
    /// narrowing, applied to the schedule instead of the chart list.</summary>
    Task<IReadOnlyList<Appointment>> ListForRangeAsync(ICurrentUser actor, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
