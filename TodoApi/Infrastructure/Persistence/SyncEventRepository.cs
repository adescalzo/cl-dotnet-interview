using Microsoft.EntityFrameworkCore;
using TodoApi.Application.Sync;
using TodoApi.Data;
using TodoApi.Data.Entities;

namespace TodoApi.Infrastructure.Persistence;

public interface ISyncEventRepository
{
    Task AddAsync(SyncEvent syncEvent, CancellationToken ct = default);
    Task<List<SyncEvent>> GetPendingAsync(int batchSize, CancellationToken ct = default);
}

public sealed class SyncEventRepository(TodoContext context) : ISyncEventRepository
{
    public async Task AddAsync(SyncEvent syncEvent, CancellationToken ct = default)
    {
        await context.SyncEvent.AddAsync(syncEvent, ct).ConfigureAwait(false);
    }

    public async Task<List<SyncEvent>> GetPendingAsync(
        int batchSize,
        CancellationToken ct = default
    )
    {
        return await context
            .SyncEvent.Where(e => e.Status == SyncStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
