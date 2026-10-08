using FluentValidation;

namespace PhysioTrac.Application.Clinical;

public class UpdateGoalProgressRequestValidator : AbstractValidator<UpdateGoalProgressRequest>
{
    public UpdateGoalProgressRequestValidator()
    {
        RuleFor(r => r.CurrentValue).GreaterThanOrEqualTo(0).When(r => r.CurrentValue.HasValue);
        RuleFor(r => r.Status).Must(s => s is null || GoalRules.IsRecordable(s.Value))
            .WithMessage("Choose Not started, In progress, Met, Partially met or Discontinued.");
        RuleFor(r => r.Comment).MaximumLength(2000);
    }
}
