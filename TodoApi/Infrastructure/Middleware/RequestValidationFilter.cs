using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TodoApi.Infrastructure.Middleware;

public sealed class RequestValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (
                context.HttpContext.RequestServices.GetService(validatorType)
                is not IValidator validator
            )
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator
                .ValidateAsync(validationContext, context.HttpContext.RequestAborted)
                .ConfigureAwait(false);

            if (!result.IsValid)
            {
                foreach (var failure in result.Errors)
                {
                    context.ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
                }
            }
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(
                new ValidationProblemDetails(context.ModelState)
            );
            return;
        }

        await next().ConfigureAwait(false);
    }
}
