using FluentValidation;

namespace PhysioTrac.Api.Controllers;

public class CreatePayerRequestValidator : AbstractValidator<CreatePayerRequest>
{
    public CreatePayerRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.TimelyFilingDays).GreaterThan(0).When(r => r.TimelyFilingDays is not null);
    }
}
