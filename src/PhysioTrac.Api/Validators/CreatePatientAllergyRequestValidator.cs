using FluentValidation;

namespace PhysioTrac.Api.Controllers;

public class CreatePatientAllergyRequestValidator : AbstractValidator<CreatePatientAllergyRequest>
{
    public CreatePatientAllergyRequestValidator()
    {
        RuleFor(r => r.Allergen).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Reaction).MaximumLength(500);
        RuleFor(r => r.Notes).MaximumLength(1000);
    }
}
