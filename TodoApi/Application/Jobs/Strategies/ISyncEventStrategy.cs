using TodoApi.Data.Entities;

namespace TodoApi.Application.Jobs.Strategies;

public interface ISyncEventStrategy
{
    bool CanHandle(SyncEvent syncEvent);
    Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct);
}
