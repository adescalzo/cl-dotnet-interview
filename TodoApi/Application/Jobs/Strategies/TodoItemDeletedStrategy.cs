using System.Net;
using System.Text.Json;
using Refit;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoItemDeletedStrategy(
    IExternalTodoApiClient client,
    ILogger<TodoItemDeletedStrategy> logger
) : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoItem, EventType: EventType.Deleted };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        try
        {
            var payload = JsonSerializer.Deserialize<TodoItemDeletedPayload>(syncEvent.Payload)!;

            try
            {
                await client
                    .DeleteTodoItemAsync(
                        syncEvent.CorrelationId.ToString(),
                        payload.TodoListId.ToString(),
                        payload.Id.ToString(),
                        ct
                    )
                    .ConfigureAwait(false);
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _ = ex;
            }
        }
        catch (Exception ex)
        {
            logger.LogTodoItemDeletedStrategyFailed(syncEvent.Id, syncEvent.EntityId, ex);
            throw;
        }
    }
}

internal static partial class TodoItemDeletedStrategyLoggerDefinition
{
    [LoggerMessage(
        EventId = 1600,
        Level = LogLevel.Error,
        EventName = "TodoItemDeletedStrategyFailed",
        Message = "TodoItemDeleted strategy failed for SyncEventId: {SyncEventId}, EntityId: {EntityId}"
    )]
    public static partial void LogTodoItemDeletedStrategyFailed(
        this ILogger logger,
        Guid syncEventId,
        Guid entityId,
        Exception ex
    );
}
