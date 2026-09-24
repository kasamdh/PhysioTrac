using Microsoft.Extensions.Logging;
using PhysioTrac.Application.Scheduling;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Logs what a real reminder integration would do instead of
/// actually sending anything -- no SMS/email provider is wired up yet. Swap
/// this registration in DependencyInjection for a real implementation
/// (Twilio/SendGrid/etc.) when one exists; AppointmentService and every
/// controller are already written against the interface, not this class.</summary>
public class NoOpReminderService : IReminderService
{
    private readonly ILogger<NoOpReminderService> _logger;

    public NoOpReminderService(ILogger<NoOpReminderService> logger)
    {
        _logger = logger;
    }

    public Task ScheduleReminderAsync(Guid appointmentId, DateTimeOffset appointmentStartsAt, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Reminder would be scheduled for appointment {AppointmentId}, visit starts {AppointmentStartsAt}.",
            appointmentId, appointmentStartsAt);
        return Task.CompletedTask;
    }

    public Task CancelReminderAsync(Guid appointmentId, CancellationToken ct = default)
    {
        _logger.LogInformation("Reminder would be cancelled for appointment {AppointmentId}.", appointmentId);
        return Task.CompletedTask;
    }
}
