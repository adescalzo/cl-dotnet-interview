using System.Text.Json;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Payloads;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoListCreatedStrategy(
    IExternalTodoApiClient client,
    ILogger<TodoListCreatedStrategy> logger
) : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoList, EventType: EventType.Created };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        try
        {
            var payload = JsonSerializer.Deserialize<TodoListCreatedPayload>(syncEvent.Payload)!;

            await client
                .CreateTodoListAsync(
                    syncEvent.CorrelationId.ToString(),
                    new CreateExternalTodoListRequest(payload.Id.ToString(), payload.Name, []),
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogTodoListCreatedStrategyFailed(syncEvent.Id, syncEvent.EntityId, ex);
            throw;
        }
    }
}

internal static partial class TodoListCreatedStrategyLoggerDefinition
{
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Error,
        EventName = "TodoListCreatedStrategyFailed",
        Message = "TodoListCreated strategy failed for SyncEventId: {SyncEventId}, EntityId: {EntityId}"
    )]
    public static partial void LogTodoListCreatedStrategyFailed(
        this ILogger logger,
        Guid syncEventId,
        Guid entityId,
        Exception ex
    );
}
