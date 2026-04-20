using System.Reflection;
using FluentValidation;
using Wolverine;
using TodoApi.Infrastructure.Mediator;

namespace TodoApi.Infrastructure.Configuration;

/// <summary>
/// Extension methods for configuring a Wolverine mediator framework.
/// Implements RULE-DEV-001: Use Wolverine for all mediator patterns.
/// </summary>
public static class WolverineExtensions
{
    /// <summary>
    /// Adds Wolverine mediator with auto-discovery and middleware configuration.
    /// Discovers handlers from the Application assembly and applies cross-cutting concerns.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="applicationAssembly">Assembly containing handlers (typically Application project)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddWolverineMediator(this IServiceCollection services, Assembly applicationAssembly)
    {
        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        services.AddWolverine(ExtensionDiscovery.ManualOnly, opts =>
        {
            opts.Discovery.IncludeAssembly(applicationAssembly);

            opts.Policies.AddMiddleware(typeof(ValidationMiddleware));
            opts.Policies.AddMiddleware<LoggingMiddleware>();
            opts.Policies.AddMiddleware<TransactionMiddleware>();
        });

        return services;
    }
}
