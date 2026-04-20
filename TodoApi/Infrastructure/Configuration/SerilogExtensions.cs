using Serilog;
using Serilog.Events;

namespace TodoApi.Infrastructure.Configuration;

/// <summary>
/// Serilog configuration extensions for request logging.
/// </summary>
public static class SerilogExtensions
{
    /// <summary>
    /// Configures Serilog request logging with appropriate log levels based on HTTP status codes.
    /// - 5xx: Error
    /// - 4xx: Warning (client errors should not be logged as errors)
    /// - 2xx/3xx: Information
    /// </summary>
    public static IApplicationBuilder UseSerilogRequestLoggingConfiguration(
        this IApplicationBuilder app
    )
    {
        app.UseSerilogRequestLogging(options =>
        {
            // Treat 4xx client errors as warnings, not errors
            options.GetLevel = (httpContext, _, ex) =>
            {
                if (ex is not null || httpContext.Response.StatusCode >= 500)
                {
                    return LogEventLevel.Error;
                }

                if (httpContext.Response.StatusCode >= 400)
                {
                    return LogEventLevel.Warning;
                }

                return LogEventLevel.Information;
            };
        });

        return app;
    }
}
