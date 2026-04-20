using System.Reflection;
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
        // Disable automatic extension discovery to prevent scanning build tools and other non-relevant assemblies
        // See: https://wolverinefx.net/guide/extensions.html#disabling-assembly-scanning
        services.AddWolverine(ExtensionDiscovery.ManualOnly, opts =>
        {
            // Auto-discover all handlers from Application assembly
            // Handlers follow convention: public class XxxHandler { public YYY Handle(XXX message) { ... } }
            opts.Discovery.IncludeAssembly(applicationAssembly);

            // Apply logging middleware to All handlers
            opts.Policies.AddMiddleware<LoggingMiddleware>();

            // Apply transaction middleware to All handlers
            opts.Policies.AddMiddleware<TransactionMiddleware>();

            // Optional: Configure additional Wolverine settings
            // opts.Durability.Mode = DurabilityMode.Solo; // For outbox pattern (future)
            // opts.LocalQueue("important").MaximumParallelMessages(5); // For background processing
        });

        return services;
    }
}
