using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Infrastructure.Configuration;

/// <summary>
/// Dependency injection configuration for the Persistence layer.
/// </summary>
public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddDbContext<TodoContext>(opt =>
        {
            opt.UseSqlServer(configuration.GetConnectionString("TodoContext"));

            // Enable detailed errors in development
            opt.EnableDetailedErrors();
            opt.EnableSensitiveDataLogging();
        });

        // Automatically register all query repositories
        // Scans for classes in the same namespace as FeeRepositoryQuery
        /*
        services.Scan(scan => scan
            .FromAssemblyOf<FeeRepositoryQuery>()
            .AddClasses(classes => classes.InNamespaceOf<FeeRepositoryQuery>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        // Automatically register all command repositories
        // Scans for classes in the same namespace as FeeRepositoryCommand
        services.Scan(scan => scan
            .FromAssemblyOf<FeeRepositoryCommand>()
            .AddClasses(classes => classes.InNamespaceOf<FeeRepositoryCommand>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());
        */

        return services;
    }
}
