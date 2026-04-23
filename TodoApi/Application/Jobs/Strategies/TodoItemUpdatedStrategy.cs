using System.Text.Json;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Payloads;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoItemUpdatedStrategy(
    IExternalTodoApiClient client,
    ILogger<TodoItemUpdatedStrategy> logger
) : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoItem, EventType: EventType.Updated };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        try
        {
            var payload = JsonSerializer.Deserialize<TodoItemUpdatedPayload>(syncEvent.Payload)!;

            await client
                .UpdateTodoItemAsync(
                    syncEvent.CorrelationId.ToString(),
                    payload.TodoListId.ToString(),
                    payload.Id.ToString(),
                    new UpdateExternalTodoItemRequest(payload.Name, payload.IsComplete),
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogTodoItemUpdatedStrategyFailed(syncEvent.Id, syncEvent.EntityId, ex);
            throw;
        }
    }
}

internal static partial class TodoItemUpdatedStrategyLoggerDefinition
{
    [LoggerMessage(
        EventId = 1500,
        Level = LogLevel.Error,
        EventName = "TodoItemUpdatedStrategyFailed",
        Message = "TodoItemUpdated strategy failed for SyncEventId: {SyncEventId}, EntityId: {EntityId}"
    )]
    public static partial void LogTodoItemUpdatedStrategyFailed(
        this ILogger logger,
        Guid syncEventId,
        Guid entityId,
        Exception ex
    );
}
