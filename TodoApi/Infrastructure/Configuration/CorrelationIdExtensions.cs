using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace TodoApi.Infrastructure.Configuration;

/// <summary>
/// Extension methods for configuring Correlation ID middleware.
/// Provides request tracing across logs and services.
/// </summary>
public static class CorrelationIdExtensions
{
    /// <summary>
    /// Header name for correlation ID.
    /// </summary>
    private const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>
    /// Adds correlation ID middleware to the pipeline.
    /// Generates or accepts a correlation ID for each request and enriches logs.
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <returns>Application builder for chaining</returns>
    /// <remarks>
    /// Should be called early in the pipeline, before logging middleware.
    /// The correlation ID is:
    /// - Read from X-Correlation-Id header if present (for distributed tracing)
    /// - Generated as a new GUID if not present
    /// - Added to response headers for client correlation
    /// - Pushed to Serilog LogContext for all logs in the request
    /// </remarks>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Use(async (context, next) =>
        {
            var correlationId = GetOrCreateCorrelationId(context);

            // Add to response headers for client-side correlation
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.TryAdd(CorrelationIdHeader, correlationId);
                return Task.CompletedTask;
            });

            // Store in HttpContext.Items for access in handlers/services
            context.Items["CorrelationId"] = correlationId;

            // Push to Serilog LogContext - all logs in this request will include it
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await next();
            }
        });

        return app;
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        // Check for incoming correlation ID header (distributed tracing from frontend/gateway)
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existingId)
            && !string.IsNullOrWhiteSpace(existingId))
        {
            return existingId.ToString();
        }

        // Generate a new UUID for this request
        return Guid.NewGuid().ToString();
    }
}
