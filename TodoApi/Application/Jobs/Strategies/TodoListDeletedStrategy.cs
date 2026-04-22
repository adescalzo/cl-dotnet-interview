using System.Net;
using System.Text.Json;
using Refit;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoListDeletedStrategy(
    IExternalTodoApiClient client,
    ISyncMappingRepository mappings
) : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoList, EventType: EventType.Deleted };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        var payload = JsonSerializer.Deserialize<TodoListDeletedPayload>(syncEvent.Payload)!;

        var mapping = await mappings
            .FindByLocalIdAsync(EntityType.TodoList, payload.Id, ct)
            .ConfigureAwait(false);

        if (mapping is null)
        {
            return;
        }

        try
        {
            await client.DeleteTodoListAsync(mapping.ExternalId, ct).ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _ = ex;
        }

        mappings.Remove(mapping);
    }
}
