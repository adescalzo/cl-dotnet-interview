using TodoApi.Application.Jobs;
using TodoApi.Application.Jobs.Strategies;

namespace TodoApi.Infrastructure.Configuration;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSingleton<IClock, Clock>();

        services.AddExternalApiClient(configuration);

        services.AddScoped<ISyncEventStrategy, TodoListCreatedStrategy>();
        services.AddScoped<ISyncEventStrategy, TodoListUpdatedStrategy>();
        services.AddScoped<ISyncEventStrategy, TodoListDeletedStrategy>();
        services.AddScoped<ISyncEventStrategy, TodoItemCreatedStrategy>();
        services.AddScoped<ISyncEventStrategy, TodoItemUpdatedStrategy>();
        services.AddScoped<ISyncEventStrategy, TodoItemDeletedStrategy>();
        services.AddScoped<SyncEventDispatcher>();

        return services;
    }
}
