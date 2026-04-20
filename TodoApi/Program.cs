using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using TodoApi.Data;
using TodoApi.Infrastructure.Configuration;
using TodoApi.Infrastructure.Hubs;
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
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    Log.Information("Starting Zea API");

    // API Configuration
    builder.Services.AddProblemDetailsConfiguration();

    // Persistency and Infrastructure

    builder
        .Services.AddDbContext<TodoContext>(opt =>
            opt.UseSqlServer(builder.Configuration.GetConnectionString("TodoContext"))
        )
        .AddEndpointsApiExplorer()
        .AddControllers();

    // Mediator (Wolverine)
    builder.Services.AddWolverineMediator(typeof(IUnitOfWork).Assembly);

    builder.Services.AddSignalR();

    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();

    builder.Services
        .AddHealthChecks()
        .AddDbContextCheck<TodoContext>(
            name: "TodoContext",
            failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy);

    // CORS Configuration
    builder.Services.AddCorsConfiguration(builder.Configuration);

    var app = builder.Build();

    // Health checks (mapped FIRST - must work even when other services fail)
    app.MapDetailedHealthChecks("/api/health");

    // Correlation ID (must be before logging to enrich all logs)
    app.UseCorrelationId();

    // Logging (capture all requests)
    app.UseSerilogRequestLoggingConfiguration();

    // CORS (must be before authentication)
    app.UseCorsConfiguration();

    app.UseAuthorization();
    app.MapControllers();

    app.MapHub<NotificatoinHub>("/notificationHub");

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
