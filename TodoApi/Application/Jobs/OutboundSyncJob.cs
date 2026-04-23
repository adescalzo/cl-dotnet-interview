using Microsoft.AspNetCore.SignalR;
using Quartz;
using TodoApi.Application.Sync;
using TodoApi.Infrastructure.Hubs;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Jobs;

[DisallowConcurrentExecution]
public sealed class OutboundSyncJob(
    IServiceScopeFactory scopeFactory,
    IHubContext<NotificationHub> hub,
    ILogger<OutboundSyncJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await using var scope = scopeFactory.CreateAsyncScope();

        var syncEventRepo = scope.ServiceProvider.GetRequiredService<ISyncEventRepository>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<SyncEventDispatcher>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var pending = await syncEventRepo
            .GetPendingAsync(50, context.CancellationToken)
            .ConfigureAwait(false);
        var coalesced = Coalesce(pending);
        var processed = 0;
        var failed = 0;

        foreach (var evt in coalesced)
        {
            try
            {
                await dispatcher
                    .DispatchAsync(evt, context.CancellationToken)
                    .ConfigureAwait(false);
                evt.MarkCompleted();
                processed++;
            }
            catch (Exception ex)
            {
                evt.MarkFailed(ex.Message);
                failed++;
                logger.LogOutboundSyncFailed(ex, evt.EntityType, evt.EventType, evt.EntityId);
            }
            finally
            {
                await uow.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
            }
        }

        if (processed + failed > 0)
        {
            await hub
                .Clients.All.SendAsync(
                    "OutboundSyncJob",
                    new { Processed = processed, Failed = failed },
                    context.CancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    private static IEnumerable<Data.Entities.SyncEvent> Coalesce(
        IEnumerable<Data.Entities.SyncEvent> events
    )
    {
        return events
            .GroupBy(e => e.EntityId)
            .Select(g => g.OrderByDescending(e => e.CreatedAt).First());
    }
}

internal static partial class OutboundSyncJobLoggerDefinition
{
    [LoggerMessage(
        EventId = 900,
        Level = LogLevel.Error,
        EventName = "OutboundSyncFailed",
        Message = "Outbound sync failed for {EntityType}/{EventType} {EntityId}"
    )]
    public static partial void LogOutboundSyncFailed(
        this ILogger logger,
        Exception ex,
        EntityType entityType,
        EventType eventType,
        Guid entityId
    );
}
