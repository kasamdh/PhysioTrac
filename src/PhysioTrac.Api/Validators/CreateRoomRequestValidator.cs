using FluentValidation;

namespace PhysioTrac.Api.Controllers;

public class CreateRoomRequestValidator : AbstractValidator<CreateRoomRequest>
{
    public CreateRoomRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(100);
    }
}
