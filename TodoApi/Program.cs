using FluentValidation;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Serilog;
using Serilog.Events;
using TodoApi.Data;
using TodoApi.Infrastructure.Configuration;
using TodoApi.Infrastructure.Hubs;
using TodoApi.Infrastructure.Middleware;
using TodoApi.Infrastructure.Persistence;

// Bootstrap logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Logging Configuration
    builder.Host.UseSerilog(
        (context, services, configuration) =>
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
    );

    Log.Information("Starting Todo API");

    // API Configuration
    builder.Services.AddProblemDetailsConfiguration();

    // Application services
    builder.Services.AddApplication(builder.Configuration);

    // Persistency and Infrastructure
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddPersistence(builder.Configuration);
    builder.Services.AddQuartzScheduler();

    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    builder
        .Services.AddEndpointsApiExplorer()
        .AddControllers(options => options.Filters.Add<RequestValidationFilter>());

    // Mediator (Wolverine)
    builder.Services.AddWolverineMediator(typeof(IUnitOfWork).Assembly);

    builder.Services.AddSignalR();

    builder
        .Services.AddHealthChecks()
        .AddDbContextCheck<TodoContext>(
            name: "TodoContext",
            failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy
        );

    // CORS Configuration
    builder.Services.AddCorsConfiguration(builder.Configuration);

    var app = builder.Build();

    await app.MigrateDatabaseAsync().ConfigureAwait(false);

    // Health checks (mapped FIRST - must work even when other services fail)
    app.MapDetailedHealthChecks("/api/health");

    // Correlation ID (must be before logging to enrich all logs)
    app.UseCorrelationId();

    // Logging (capture all requests)
    app.UseSerilogRequestLoggingConfiguration();

    // CORS (must be before authentication)
    app.UseCorsConfiguration();

    app.MapControllers();

    app.MapHub<NotificationHub>("/notificationHub");

    // Log the bound listener URLs once Kestrel has actually started.
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var addresses = app
            .Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()
            ?.Addresses;

        if (addresses is { Count: > 0 })
        {
            Log.Information("Todo API listening on: {Addresses}", addresses);
            return;
        }

        Log.Information("Todo API started, but no server addresses were reported.");
    });

    await app.RunAsync().ConfigureAwait(false);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync().ConfigureAwait(false);
}
