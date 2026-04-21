using System.Reflection;
using TodoApi.Infrastructure.Mediator;
using Wolverine;

namespace TodoApi.Infrastructure.Configuration;

public static class WolverineExtensions
{
    public static IServiceCollection AddWolverineMediator(
        this IServiceCollection services,
        Assembly applicationAssembly
    )
    {
        services.AddWolverine(
            ExtensionDiscovery.ManualOnly,
            opts =>
            {
                opts.Discovery.IncludeAssembly(applicationAssembly);

                opts.Policies.AddMiddleware<LoggingMiddleware>();
                opts.Policies.AddMiddleware<TransactionMiddleware>();
            }
        );

        return services;
    }
}
