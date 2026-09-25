using FluentValidation;

namespace PhysioTrac.Api.Controllers;

public class CreateCptCodeMappingRequestValidator : AbstractValidator<CreateCptCodeMappingRequest>
{
    public CreateCptCodeMappingRequestValidator()
    {
        RuleFor(r => r.CptCode).NotEmpty().Matches("^[A-Z0-9]{5}$").WithMessage("Enter a 5-character CPT/HCPCS code (e.g. 97110).");
    }
}
