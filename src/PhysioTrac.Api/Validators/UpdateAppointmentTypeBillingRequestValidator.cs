using FluentValidation;

namespace PhysioTrac.Api.Controllers;

public class UpdateAppointmentTypeBillingRequestValidator : AbstractValidator<UpdateAppointmentTypeBillingRequest>
{
    public UpdateAppointmentTypeBillingRequestValidator()
    {
        RuleFor(r => r.DefaultCptCode)
            .Matches("^[A-Z0-9]{5}$").WithMessage("Enter a 5-character CPT/HCPCS code (e.g. 97110).")
            .When(r => r.DefaultCptCode is not null);
        RuleFor(r => r.Price).GreaterThanOrEqualTo(0).When(r => r.Price is not null);
    }
}
