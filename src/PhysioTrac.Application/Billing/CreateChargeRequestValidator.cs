using FluentValidation;

namespace PhysioTrac.Application.Billing;

/// <summary>Defense-in-depth, not a replacement: ChargeService.CreateAsync
/// already enforces these same CPT-code/modifier format rules itself (and
/// must keep doing so, since it's called directly by tests and can be
/// called by future internal code that never goes through this Api-layer
/// validator). This validator exists so a malformed request is rejected at
/// the API boundary -- before a transaction, a provider row lock, or a
/// database round-trip -- rather than deep inside the service.</summary>
public class CreateChargeRequestValidator : AbstractValidator<CreateChargeRequest>
{
    public CreateChargeRequestValidator()
    {
        RuleFor(r => r.PatientId).NotEmpty();
        RuleFor(r => r.ProviderId).NotEmpty();
        RuleFor(r => r.CptCode)
            .NotEmpty()
            .Matches("^[A-Z0-9]{5}$").WithMessage("Enter a 5-character CPT/HCPCS code (e.g. 97110).");
        RuleForEach(r => r.Modifiers)
            .Matches("^[A-Z0-9]{2}$").WithMessage("Modifiers must each be 2 characters (e.g. GP, 59).")
            .When(r => r.Modifiers is not null);
        RuleFor(r => r.Modifiers)
            .Must(m => m is null || m.Count <= 4).WithMessage("Enter at most 4 modifiers.");
        RuleFor(r => r.Units).GreaterThan(0);
        RuleFor(r => r.ChargeAmount).GreaterThanOrEqualTo(0);
    }
}
