using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Booking;

/// <summary>Direct port of the patient-portal half of `care/booking.py` —
/// same re-validation discipline as the public surface, but scoped to an
/// already-known, already-authenticated <see cref="Patient"/> resolved via
/// <c>ITenantAccessService.RequirePortalPatientAsync</c>, never a
/// client-supplied patient id.</summary>
public interface IPortalBookingService
{
    Task<Appointment> CreateAsync(ICurrentUser patientUser, PortalBookingRequest request, CancellationToken ct = default);

    /// <summary>Throws <see cref="ChangeCutoffException"/> if within the
    /// organization's patient-change cutoff window.</summary>
    Task<Appointment> CancelAsync(ICurrentUser patientUser, Guid appointmentId, CancellationToken ct = default);

    Task<Appointment> RescheduleAsync(ICurrentUser patientUser, Guid appointmentId, PortalRescheduleRequest request, CancellationToken ct = default);

    Task<Appointment> ConfirmAsync(ICurrentUser patientUser, Guid appointmentId, CancellationToken ct = default);

    /// <summary>Idempotent: re-submitting the same preferences while already
    /// active just returns the existing entry rather than duplicating it.</summary>
    Task<Waitlist> JoinWaitlistAsync(ICurrentUser patientUser, JoinWaitlistRequest request, CancellationToken ct = default);

    Task<Waitlist> LeaveWaitlistAsync(ICurrentUser patientUser, Guid entryId, CancellationToken ct = default);

    Task<IReadOnlyList<Waitlist>> ListWaitlistAsync(ICurrentUser patientUser, CancellationToken ct = default);
}
