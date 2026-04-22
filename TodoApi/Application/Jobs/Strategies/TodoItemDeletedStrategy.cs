using System.Net;
using System.Text.Json;
using Refit;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoItemDeletedStrategy(
    IExternalTodoApiClient client,
    ISyncMappingRepository mappings
) : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoItem, EventType: EventType.Deleted };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        var payload = JsonSerializer.Deserialize<TodoItemDeletedPayload>(syncEvent.Payload)!;
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

        try
        {
            await client
                .DeleteTodoItemAsync(listMapping.ExternalId, itemMapping.ExternalId, ct)
                .ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _ = ex;
        }

        mappings.Remove(itemMapping);
    }
}
