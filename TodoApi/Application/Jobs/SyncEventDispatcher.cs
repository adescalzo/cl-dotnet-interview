using TodoApi.Application.Jobs.Strategies;
using TodoApi.Data.Entities;

namespace TodoApi.Application.Jobs;

public sealed class SyncEventDispatcher(IEnumerable<ISyncEventStrategy> strategies)
{
    public async Task DispatchAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        var strategy =
            strategies.FirstOrDefault(s => s.CanHandle(syncEvent))
            ?? throw new InvalidOperationException(
                $"No strategy registered for {syncEvent.EntityType}/{syncEvent.EventType}"
            );

        await strategy.ExecuteAsync(syncEvent, ct).ConfigureAwait(false);
    }
}
