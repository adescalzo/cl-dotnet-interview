using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quartz;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Payloads;
using TodoApi.Data;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Hubs;
using TodoApi.Infrastructure.Persistence;
using TodoApi.Infrastructure.Settings;

namespace TodoApi.Application.Jobs;

[DisallowConcurrentExecution]
public sealed class InboundSyncJob(
    IServiceScopeFactory scopeFactory,
    IHubContext<NotificationHub> hub,
    IOptions<ProcessOptions> optionsAccessor,
    ILogger<InboundSyncJob> logger
) : IJob
{
    private readonly ProcessOptions _options = optionsAccessor.Value;

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await using var scope = scopeFactory.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<IExternalTodoApiClient>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var dbContext = scope.ServiceProvider.GetRequiredService<TodoContext>();

        var externalLists = await client
            .GetAllAsync(context.CancellationToken)
            .ConfigureAwait(false);
        var synced = 0;
        var batchSize = _options.BatchSizeInbound;
        var sinceLastFlush = 0;

        foreach (var externalList in externalLists)
        {
            try
            {
                synced += await SyncListAsync(
                        externalList,
                        dbContext,
                        clock,
                        context.CancellationToken
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogInboundSyncFailed(ex, externalList.Id);
            }

            sinceLastFlush++;
            if (sinceLastFlush < batchSize)
            {
                continue;
            }

            await uow.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
            sinceLastFlush = 0;
        }

        if (sinceLastFlush > 0)
        {
            await uow.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
        }

        if (synced == 0)
        {
            return;
        }

        await hub
            .Clients.All.SendAsync(
                "InboundSyncJob",
                new { Synced = synced },
                context.CancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<int> SyncListAsync(
        ExternalTodoList externalList,
        TodoContext dbContext,
        IClock clock,
        CancellationToken ct
    )
    {
        var localList = await ResolveLocalListAsync(externalList, dbContext, ct)
            .ConfigureAwait(false);
        var synced = 0;
        var now = clock.UtcNow;

        if (localList is null)
        {
            localList = new TodoList(externalList.Name, now);
            localList.LinkExternal(externalList.Id);
            await dbContext.TodoList.AddAsync(localList, ct).ConfigureAwait(false);
            synced++;
        }
        else
        {
            if (localList.ExternalId is null)
            {
                localList.LinkExternal(externalList.Id);
            }

            if (!string.Equals(localList.Name, externalList.Name, StringComparison.Ordinal))
            {
                localList.Update(externalList.Name, now);
                synced++;
            }
        }

        var order = localList.Items.Count;
        foreach (var externalItem in externalList.Items)
        {
            if (IsAlreadyLinked(localList, externalItem))
            {
                continue;
            }

            order++;
            var item = localList.AddItem(externalItem.Description, order, now);
            if (externalItem.Completed)
            {
                item.Complete(now);
            }
            item.LinkExternal(externalItem.Id);
            synced++;
        }

        return synced;
    }

    private static async Task<TodoList?> ResolveLocalListAsync(
        ExternalTodoList externalList,
        TodoContext dbContext,
        CancellationToken ct
    )
    {
        if (Guid.TryParse(externalList.SourceId, out var sourceLocalId))
        {
            var bySource = await dbContext
                .TodoList.Include(l => l.Items)
                .FirstOrDefaultAsync(l => l.Id == sourceLocalId, ct)
                .ConfigureAwait(false);
            if (bySource is not null)
            {
                return bySource;
            }
        }

        return await dbContext
            .TodoList.Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.ExternalId == externalList.Id, ct)
            .ConfigureAwait(false);
    }

    private static bool IsAlreadyLinked(TodoList list, ExternalTodoItem externalItem)
    {
        if (list.Items.Any(i => i.ExternalId == externalItem.Id))
        {
            return true;
        }

        if (
            Guid.TryParse(externalItem.SourceId, out var localId)
            && list.Items.Any(i => i.Id == localId)
        )
        {
            return true;
        }

        return false;
    }
}

internal static partial class InboundSyncJobLoggerDefinition
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        EventName = "InboundSyncFailed",
        Message = "Inbound sync failed for external list {ExternalId}"
    )]
    public static partial void LogInboundSyncFailed(
        this ILogger logger,
        Exception ex,
        string externalId
    );
}
