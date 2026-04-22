using System.Text.Json;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Dtos;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoListUpdatedStrategy(
    IExternalTodoApiClient client,
    ISyncMappingRepository mappings
) : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoList, EventType: EventType.Updated };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        var payload = JsonSerializer.Deserialize<TodoListUpdatedPayload>(syncEvent.Payload)!;
        var mapping = await mappings
            .FindByLocalIdAsync(EntityType.TodoList, payload.Id, ct)
            .ConfigureAwait(false);

        if (mapping is null)
        {
            return;
        }

        var result = await client
            .UpdateTodoListAsync(
                mapping.ExternalId,
                new UpdateExternalTodoListRequest(payload.Name),
                ct
            )
            .ConfigureAwait(false);

        mapping.UpdateSync(mapping.ExternalId, result.UpdatedAt);
    }
}
