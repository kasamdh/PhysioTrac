using FluentValidation;

namespace PhysioTrac.Api.Controllers;

public class CreateSuperbillRequestValidator : AbstractValidator<CreateSuperbillRequest>
{
    public CreateSuperbillRequestValidator()
    {
        RuleFor(r => r.PatientId).NotEmpty();
        RuleFor(r => r.ClinicianId).NotEmpty();
        RuleFor(r => r.ServiceDate).NotEqual(default(DateOnly));
        RuleFor(r => r.Amount).GreaterThanOrEqualTo(0);
    }
}
