using System.Net;
using System.Text.Json;
using Refit;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoListDeletedStrategy(
    IExternalTodoApiClient client,
    ILogger<TodoListDeletedStrategy> logger
) : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoList, EventType: EventType.Deleted };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        try
        {
            var payload = JsonSerializer.Deserialize<TodoListDeletedPayload>(syncEvent.Payload)!;

            await client
                .DeleteTodoListAsync(
                    syncEvent.CorrelationId.ToString(),
                    payload.Id.ToString(),
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _ = ex;
        }
        catch (Exception ex)
        {
            logger.LogTodoListDeletedStrategyFailed(syncEvent.Id, syncEvent.EntityId, ex);
            throw;
        }
    }
}

internal static partial class TodoListDeletedStrategyLoggerDefinition
{
    [LoggerMessage(
        EventId = 1300,
        Level = LogLevel.Error,
        EventName = "TodoListDeletedStrategyFailed",
        Message = "TodoListDeleted strategy failed for SyncEventId: {SyncEventId}, EntityId: {EntityId}"
    )]
    public static partial void LogTodoListDeletedStrategyFailed(
        this ILogger logger,
        Guid syncEventId,
        Guid entityId,
        Exception ex
    );
}
