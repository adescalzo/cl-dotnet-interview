using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Quartz;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Dtos;
using TodoApi.Application.Sync;
using TodoApi.Data;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Extensions;
using TodoApi.Infrastructure.Hubs;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Jobs;

[DisallowConcurrentExecution]
public sealed class InboundSyncJob(
    IServiceScopeFactory scopeFactory,
    IHubContext<NotificationHub> hub,
    ILogger<InboundSyncJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await using var scope = scopeFactory.CreateAsyncScope();

        var client = scope.ServiceProvider.GetRequiredService<IExternalTodoApiClient>();
        var mappings = scope.ServiceProvider.GetRequiredService<ISyncMappingRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var dbContext = scope.ServiceProvider.GetRequiredService<TodoContext>();

        var externalLists = await client.GetAllAsync(context.CancellationToken).ConfigureAwait(false);
        var synced = 0;

        foreach (var externalList in externalLists)
        {
            try
            {
                synced += await SyncListAsync(
                        externalList,
                        dbContext,
                        mappings,
                        clock,
                        context.CancellationToken
                    )
                    .ConfigureAwait(false);

                await uow.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogInboundSyncFailed(ex, externalList.Id);
            }
        }

        if (synced == 0)
        {
            return;
        }

        await hub
            .Clients.All.SendAsync("InboundSyncJob", new { Synced = synced }, context.CancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<int> SyncListAsync(
        ExternalTodoList externalList,
        TodoContext dbContext,
        ISyncMappingRepository mappings,
        IClock clock,
        CancellationToken ct
    )
    {
        var synced = 0;
        var listMapping = await mappings
            .FindByExternalIdAsync(EntityType.TodoList, externalList.Id, ct)
            .ConfigureAwait(false);

        TodoList localList;

        if (listMapping is null)
        {
            localList = new TodoList(externalList.Name, clock.UtcNow);
            await dbContext.TodoList.AddAsync(localList, ct).ConfigureAwait(false);
            await mappings
                .AddAsync(
                    new SyncMapping(
                        EntityType.TodoList,
                        localList.Id,
                        externalList.Id,
                        externalList.UpdatedAt
                    ),
                    ct
                )
                .ConfigureAwait(false);
            synced++;
        }
        else
        {
            localList = await dbContext
                .TodoList.Include(l => l.Items)
                .FirstAsync(l => l.Id == listMapping.LocalId, ct)
                .ConfigureAwait(false);

            if (externalList.UpdatedAt > listMapping.LastSyncedAt)
            {
                localList.Update(externalList.Name, clock.UtcNow);
                listMapping.UpdateSync(externalList.Id, externalList.UpdatedAt);
                synced++;
            }
        }

        foreach (var externalItem in externalList.TodoItems)
        {
            synced += await SyncItemAsync(externalItem, localList, dbContext, mappings, clock, ct)
                .ConfigureAwait(false);
        }

        return synced;
    }

    private static async Task<int> SyncItemAsync(
        ExternalTodoItem externalItem,
        TodoList localList,
        TodoContext dbContext,
        ISyncMappingRepository mappings,
        IClock clock,
        CancellationToken ct
    )
    {
        var itemMapping = await mappings
            .FindByExternalIdAsync(EntityType.TodoItem, externalItem.Id, ct)
            .ConfigureAwait(false);

        if (itemMapping is not null)
        {
            return 0;
        }

        var now = clock.UtcNow;
        var newItem = localList.AddItem(GuidV7.NewGuid(), externalItem.Description, 0, now, now);
        if (externalItem.Completed)
        {
            newItem.Complete(now);
        }

        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        await mappings
            .AddAsync(
                new SyncMapping(
                    EntityType.TodoItem,
                    newItem.Id,
                    externalItem.Id,
                    externalItem.UpdatedAt
                ),
                ct
            )
            .ConfigureAwait(false);

        return 1;
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
