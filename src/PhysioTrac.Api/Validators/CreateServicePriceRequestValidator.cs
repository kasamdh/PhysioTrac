using FluentValidation;

namespace PhysioTrac.Api.Controllers;

public class CreateServicePriceRequestValidator : AbstractValidator<CreateServicePriceRequest>
{
    public CreateServicePriceRequestValidator()
    {
        RuleFor(r => r.CptCode).NotEmpty().Matches("^[A-Z0-9]{5}$").WithMessage("Enter a 5-character CPT/HCPCS code (e.g. 97110).");
        RuleFor(r => r.Label).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Price).GreaterThanOrEqualTo(0);
        RuleFor(r => r.DepositAmount).GreaterThanOrEqualTo(0).When(r => r.DepositAmount is not null);
    }
}
