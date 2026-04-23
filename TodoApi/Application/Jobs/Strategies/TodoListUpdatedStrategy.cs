using System.Text.Json;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Payloads;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoListUpdatedStrategy(
    IExternalTodoApiClient client,
    ILogger<TodoListUpdatedStrategy> logger
) : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoList, EventType: EventType.Updated };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        try
        {
            var payload = JsonSerializer.Deserialize<TodoListUpdatedPayload>(syncEvent.Payload)!;

            await client
                .UpdateTodoListAsync(
                    syncEvent.CorrelationId.ToString(),
                    payload.Id.ToString(),
                    new UpdateExternalTodoListRequest(payload.Name),
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogTodoListUpdatedStrategyFailed(syncEvent.Id, syncEvent.EntityId, ex);
            throw;
        }
    }
}

internal static partial class TodoListUpdatedStrategyLoggerDefinition
{
    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Error,
        EventName = "TodoListUpdatedStrategyFailed",
        Message = "TodoListUpdated strategy failed for SyncEventId: {SyncEventId}, EntityId: {EntityId}"
    )]
    public static partial void LogTodoListUpdatedStrategyFailed(
        this ILogger logger,
        Guid syncEventId,
        Guid entityId,
        Exception ex
    );
}
