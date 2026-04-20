using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;
using TodoApi.Infrastructure.Extensions;

namespace TodoApi.Infrastructure.Configuration;

/// <summary>
/// ProblemDetails configuration extensions for standardized error responses.
/// Implements RFC 7807 (Problem Details for HTTP APIs).
/// </summary>
public static class ProblemDetailsExtensions
{
    public static IServiceCollection AddProblemDetailsConfiguration(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            // Customize problem details generation
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance = context.HttpContext.Request.Path;

                // Add correlation ID for debugging (consistent with logs)
                context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.GetCorrelationId();
                // Add trace ID for debugging
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                // Add timestamp
                var dateTime = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
                context.ProblemDetails.Extensions["timestamp"] = dateTime;

                // Handle BadHttpRequestException specially
                var exceptionFeature = context.HttpContext.Features.Get<IExceptionHandlerFeature>();
                if (exceptionFeature?.Error is BadHttpRequestException badRequestEx)
                {
                    context.ProblemDetails.Status = badRequestEx.StatusCode;
                    context.ProblemDetails.Title = "Bad Request";
                    context.ProblemDetails.Detail = badRequestEx.Message;
                    context.ProblemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
                }
            };
        });

        return services;
    }
}
