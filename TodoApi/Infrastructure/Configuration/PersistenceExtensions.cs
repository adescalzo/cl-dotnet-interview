using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Infrastructure.Configuration;

/// <summary>
/// Dependency injection configuration for the Persistence layer.
/// </summary>
public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddDbContext<TodoContext>(opt =>
        {
            opt.UseSqlServer(configuration.GetConnectionString("TodoContext"));

            // Enable detailed errors in development
            opt.EnableDetailedErrors();
            opt.EnableSensitiveDataLogging();
        });

        // Scan for all concrete repository implementations (one per aggregate root).
        // Scrutor picks up closed types implementing IRepositoryCommand<> / IRepositoryQuery<>
        // and registers them against their interfaces. Open-generic base classes are skipped.
        services.Scan(scan =>
            scan.FromAssemblyOf<TodoListRepositoryCommand>()
                .AddClasses(classes => classes.AssignableTo(typeof(IRepositoryCommand<>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime()
        );

        services.Scan(scan =>
            scan.FromAssemblyOf<TodoListRepositoryQuery>()
                .AddClasses(classes => classes.AssignableTo(typeof(IRepositoryQuery<>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime()
        );

        services.AddScoped<ISyncEventRepository, SyncEventRepository>();
        services.AddScoped<ISyncMappingRepository, SyncMappingRepository>();

        return services;
    }
}
