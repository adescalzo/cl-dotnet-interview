namespace TodoApi.Infrastructure.Extensions;

/// <summary>
/// Extension methods for accessing correlation ID from HttpContext.
/// </summary>
public static class HttpContextCorrelationIdExtensions
{
    /// <summary>
    /// Gets the correlation ID for the current request.
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>Correlation ID or null if middleware not configured</returns>
    public static string? GetCorrelationId(this HttpContext context)
    {
        return context.Items.TryGetValue("CorrelationId", out var correlationId)
            ? correlationId?.ToString()
            : null;
    }
}
