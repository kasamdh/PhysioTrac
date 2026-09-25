using FluentValidation;

namespace PhysioTrac.Api.Controllers;

public class CreatePatientPaymentRequestValidator : AbstractValidator<CreatePatientPaymentRequest>
{
    public CreatePatientPaymentRequestValidator()
    {
        RuleFor(r => r.Amount).GreaterThan(0);
        RuleFor(r => r.ProcessorReference).MaximumLength(200);
        RuleFor(r => r.FailureMessage).MaximumLength(500);
    }
}
