using TodoApi.Application.Services;

namespace TodoApi.Infrastructure.Configuration;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IBulkOperationTracker, BulkOperationTracker>();

        return services;
    }
}