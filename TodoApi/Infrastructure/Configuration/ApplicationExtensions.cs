using TodoApi.Application.Services;
using TodoApi.Infrastructure.Settings;

namespace TodoApi.Infrastructure.Configuration;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        // General services
        services.AddSingleton<IBulkOperationTracker, BulkOperationTracker>();

        // Options
        services.Configure<ProcessOptions>(configuration.GetSection(ProcessOptions.SectionName));

        return services;
    }
}
