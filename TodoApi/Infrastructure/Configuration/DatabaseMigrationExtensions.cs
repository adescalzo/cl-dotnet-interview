using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

namespace TodoApi.Infrastructure.Configuration;

public static class DatabaseMigrationExtensions
{
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TodoContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TodoContext>>();

        try
        {
            var pending = await db.Database.GetPendingMigrationsAsync().ConfigureAwait(false);
            var pendingList = pending.ToList();

            if (pendingList.Count == 0)
            {
                logger.LogDatabaseUpToDate();
                return;
            }

            logger.LogApplyingMigrations(pendingList.Count);
            await db.Database.MigrateAsync().ConfigureAwait(false);
            logger.LogMigrationsApplied(pendingList.Count);
        }
        catch (Exception ex)
        {
            logger.LogMigrationFailed(ex);
            throw;
        }
    }
}

internal static partial class DatabaseMigrationLoggerDefinition
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        EventName = "DatabaseUpToDate",
        Message = "Database is up to date — no pending migrations"
    )]
    public static partial void LogDatabaseUpToDate(this ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        EventName = "ApplyingMigrations",
        Message = "Applying {Count} pending migration(s)"
    )]
    public static partial void LogApplyingMigrations(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        EventName = "MigrationsApplied",
        Message = "Successfully applied {Count} migration(s)"
    )]
    public static partial void LogMigrationsApplied(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Critical,
        EventName = "MigrationFailed",
        Message = "Database migration failed — application cannot start"
    )]
    public static partial void LogMigrationFailed(this ILogger logger, Exception ex);
}
