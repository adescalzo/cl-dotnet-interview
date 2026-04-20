using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TodoApi.Infrastructure.Configuration;

/// <summary>
/// Extension methods for configuring health check responses.
/// Provides a detailed JSON response instead of plain text.
/// </summary>
public static class HealthCheckExtensions
{
    public static IEndpointRouteBuilder MapDetailedHealthChecks(this IEndpointRouteBuilder app, string path)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks(path, new HealthCheckOptions
        {
            ResponseWriter = WriteDetailedHealthCheckResponse,
            AllowCachingResponses = false,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        });

        return app;
    }

    private static Task WriteDetailedHealthCheckResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var options = new JsonWriterOptions { Indented = true };

        using var memoryStream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memoryStream, options))
        {
            writer.WriteStartObject();
            writer.WriteString("status", report.Status.ToString());
            writer.WriteStartObject("results");

            foreach (var entry in report.Entries)
            {
                writer.WriteStartObject(entry.Key);
                writer.WriteString("status", entry.Value.Status.ToString());
                writer.WriteString("description", entry.Value.Description);
                writer.WriteStartObject("data");

                foreach (var item in entry.Value.Data)
                {
                    writer.WritePropertyName(item.Key);
                    JsonSerializer.Serialize(writer, item.Value, item.Value.GetType());
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return context.Response.WriteAsync(System.Text.Encoding.UTF8.GetString(memoryStream.ToArray()));
    }
}
