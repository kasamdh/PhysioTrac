using FluentValidation;

namespace PhysioTrac.Application.Clinical;

public class UpdateGoalProgressRequestValidator : AbstractValidator<UpdateGoalProgressRequest>
{
    public UpdateGoalProgressRequestValidator()
    {
        RuleFor(r => r.CurrentValue).GreaterThanOrEqualTo(0);
    }
}
