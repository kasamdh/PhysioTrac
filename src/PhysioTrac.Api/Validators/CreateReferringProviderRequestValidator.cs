using FluentValidation;

namespace PhysioTrac.Api.Controllers;

public class CreateReferringProviderRequestValidator : AbstractValidator<CreateReferringProviderRequest>
{
    public CreateReferringProviderRequestValidator()
    {
        RuleFor(r => r.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(r => r.LastName).NotEmpty().MaximumLength(100);
        RuleFor(r => r.Npi).Matches("^\\d{10}$").WithMessage("NPI must be exactly 10 digits.").When(r => r.Npi is not null);
        RuleFor(r => r.Email).EmailAddress().When(r => !string.IsNullOrEmpty(r.Email));
    }
}
