using System.Text.Json;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Payloads;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoItemCreatedStrategy(IExternalTodoApiClient client, ILogger<TodoItemCreatedStrategy> logger)
    : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoItem, EventType: EventType.Created };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        try
        {
            var payload = JsonSerializer.Deserialize<TodoItemCreatedPayload>(syncEvent.Payload)!;

            var request = new UpdateExternalTodoListRequest(
                Name: payload.TodoListName,
                Items: [new CreateExternalTodoItemRequest(payload.Id.ToString(), payload.Name, payload.IsComplete)]
            );

            await client
                .UpdateTodoListAsync(syncEvent.CorrelationId.ToString(), payload.TodoListId.ToString(), request, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogTodoItemCreatedStrategyFailed(syncEvent.Id, syncEvent.EntityId, ex);
            throw;
        }
    }
}

internal static partial class TodoItemCreatedStrategyLoggerDefinition
{
    [LoggerMessage(
        EventId = 1400,
        Level = LogLevel.Error,
        EventName = "TodoItemCreatedStrategyFailed",
        Message = "TodoItemCreated strategy failed for SyncEventId: {SyncEventId}, EntityId: {EntityId}"
    )]
    public static partial void LogTodoItemCreatedStrategyFailed(
        this ILogger logger,
        Guid syncEventId,
        Guid entityId,
        Exception ex
    );
}
