namespace PhysioTrac.Application.Scheduling;

/// <summary>Integration point for appointment reminders -- deliberately just
/// an interface plus a logging no-op implementation (NoOpReminderService)
/// today. A real SMS/email provider (Twilio, SendGrid, etc.) becomes a new
/// implementation registered in DI; nothing in AppointmentService or any
/// controller needs to change to adopt one.</summary>
public interface IReminderService
{
    /// <summary>Called once, right after an appointment is booked (or
    /// rescheduled to a new time) — a real implementation would schedule a
    /// reminder to fire some interval before StartsAt, not send anything
    /// immediately.</summary>
    Task ScheduleReminderAsync(Guid appointmentId, DateTimeOffset appointmentStartsAt, CancellationToken ct = default);

    /// <summary>Called when an appointment that had a pending reminder is
    /// cancelled, so a real implementation can cancel the scheduled send.</summary>
    Task CancelReminderAsync(Guid appointmentId, CancellationToken ct = default);
}
