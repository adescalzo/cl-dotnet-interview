using Microsoft.EntityFrameworkCore;
using TodoApi.Application.Sync;
using TodoApi.Data;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Tests.TestSupport;

internal sealed class SyncEventCommandRepository(TodoContext context) : ISyncEventRepository
{
    public async Task AddAsync(SyncEvent syncEvent, CancellationToken ct = default) =>
        await context.SyncEvent.AddAsync(syncEvent, ct).ConfigureAwait(false);

    public async Task<List<SyncEvent>> GetPendingAsync(
        int batchSize,
        CancellationToken ct = default
    ) =>
        await context
            .SyncEvent.Where(e => e.Status == SyncStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);
}
