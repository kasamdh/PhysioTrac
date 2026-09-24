using FluentValidation;

namespace PhysioTrac.Application.Patients;

/// <summary>Genuinely new validation -- PatientsController.Create previously
/// had none at all beyond the RequireRole check, so any blank name or
/// nonsensical date of birth would have been persisted as-is.</summary>
public class CreatePatientRequestValidator : AbstractValidator<CreatePatientRequest>
{
    public CreatePatientRequestValidator()
    {
        RuleFor(r => r.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(r => r.LastName).NotEmpty().MaximumLength(100);
        RuleFor(r => r.DateOfBirth)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Date of birth cannot be in the future.")
            .GreaterThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-130)).WithMessage("Date of birth is not plausible.");
        RuleFor(r => r.Email).EmailAddress().When(r => !string.IsNullOrWhiteSpace(r.Email));
        RuleFor(r => r.Phone).MaximumLength(30).When(r => !string.IsNullOrWhiteSpace(r.Phone));
        RuleFor(r => r.Address).MaximumLength(300).When(r => !string.IsNullOrWhiteSpace(r.Address));
        RuleFor(r => r.EmergencyContact).MaximumLength(200).When(r => !string.IsNullOrWhiteSpace(r.EmergencyContact));
        RuleFor(r => r.PreferredLanguage).MaximumLength(60).When(r => !string.IsNullOrWhiteSpace(r.PreferredLanguage));
    }
}
