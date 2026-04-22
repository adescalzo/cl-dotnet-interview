using System.Text.Json;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Dtos;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoItemUpdatedStrategy(
    IExternalTodoApiClient client,
    ISyncMappingRepository mappings
) : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoItem, EventType: EventType.Updated };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        var payload = JsonSerializer.Deserialize<TodoItemUpdatedPayload>(syncEvent.Payload)!;
        var listMapping = await mappings
            .FindByLocalIdAsync(EntityType.TodoList, payload.TodoListId, ct)
            .ConfigureAwait(false);

        if (listMapping is null)
        {
            return;
        }

        var itemMapping = await mappings
            .FindByLocalIdAsync(EntityType.TodoItem, payload.Id, ct)
            .ConfigureAwait(false);

        if (itemMapping is null)
        {
            return;
        }

        var result = await client
            .UpdateTodoItemAsync(
                listMapping.ExternalId,
                itemMapping.ExternalId,
                new UpdateExternalTodoItemRequest(payload.Name, payload.IsComplete),
                ct
            )
            .ConfigureAwait(false);

        itemMapping.UpdateSync(itemMapping.ExternalId, result.UpdatedAt);
    }
}
