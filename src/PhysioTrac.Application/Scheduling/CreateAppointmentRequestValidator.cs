using FluentValidation;

namespace PhysioTrac.Application.Scheduling;

/// <summary>Redundant safety net: AppointmentService.CreateAsync already
/// rejects EndsAt &lt;= StartsAt itself, and must keep doing so since it's
/// called directly by tests. This validator just moves the same rejection
/// to the API boundary so a malformed request never reaches the service's
/// double-booking/slot-lookup logic in the first place.</summary>
public class CreateAppointmentRequestValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentRequestValidator()
    {
        RuleFor(r => r.PatientId).NotEmpty();
        RuleFor(r => r.TherapistId).NotEmpty();
        RuleFor(r => r.EndsAt).GreaterThan(r => r.StartsAt).WithMessage("End time must be after start time.");
    }
}
