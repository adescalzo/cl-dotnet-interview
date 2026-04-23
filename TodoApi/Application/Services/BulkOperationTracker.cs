using System.Collections.Concurrent;

namespace TodoApi.Application.Services;

public interface IBulkOperationTracker
{
    void Start(Guid listId);
    void Stop(Guid listId);
    bool IsRunning(Guid listId);
}

public sealed class BulkOperationTracker : IBulkOperationTracker
{
    private readonly ConcurrentDictionary<Guid, byte> _running = new();

    public void Start(Guid listId) => _running.TryAdd(listId, 0);

    public void Stop(Guid listId) => _running.TryRemove(listId, out _);

    public bool IsRunning(Guid listId) => _running.ContainsKey(listId);
}