using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PhysioTrac.Api.Filters;

/// <summary>FluentValidation 11+ dropped its automatic ASP.NET Core MVC
/// integration, so this replaces it: for each action argument that has a
/// registered IValidator&lt;T&gt;, validate it and short-circuit with 422 on
/// failure. This runs before the controller action body, alongside (not
/// instead of) whatever validation the target service already does --
/// service-level checks stay in place for callers that bypass the Api
/// layer entirely (unit tests, future internal callers).</summary>
public class FluentValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                context.Result = new UnprocessableEntityObjectResult(new
                {
                    detail = "One or more validation errors occurred.",
                    errors = result.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }),
                });
                return;
            }
        }

        await next();
    }
}
