using System.Text.Json;
using System.Text.Json.Serialization;
using Refit;
using TodoApi.Application.ExternalApi;

namespace TodoApi.Infrastructure.Configuration;

public static class ExternalApiExtensions
{
    public static IServiceCollection AddExternalApiClient(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var refitSettings = new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                }
            ),
        };

        var maxRetries = configuration.GetValue("ExternalApi:MaxRetries", defaultValue: 3);
        var timeoutSeconds = configuration.GetValue("ExternalApi:TimeoutSeconds", defaultValue: 10);

        services
            .AddRefitClient<IExternalTodoApiClient>(refitSettings)
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(configuration["ExternalApi:BaseUrl"]!);
                c.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            })
            .AddPolicyHandler(_ => ResiliencePolicies.RetryPolicy(maxRetries))
            .AddPolicyHandler(ResiliencePolicies.CircuitBreakerPolicy());

        return services;
    }
}
