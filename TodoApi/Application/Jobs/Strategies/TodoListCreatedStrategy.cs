using System.Text.Json;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Dtos;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoListCreatedStrategy(
    IExternalTodoApiClient client,
    ISyncMappingRepository mappings
) : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoList, EventType: EventType.Created };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<TodoListCreatedPayload>(syncEvent.Payload)!;

        var existing = await mappings
            .FindByLocalIdAsync(EntityType.TodoList, payload.Id, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return;
        }

        var result = await client
            .CreateTodoListAsync(new CreateExternalTodoListRequest(payload.Name), ct)
            .ConfigureAwait(false);

        await mappings
            .AddAsync(
                new SyncMapping(EntityType.TodoList, payload.Id, result.Id, result.UpdatedAt),
                ct
            )
            .ConfigureAwait(false);
    }
}
