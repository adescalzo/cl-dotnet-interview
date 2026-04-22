using Microsoft.EntityFrameworkCore;
using TodoApi.Application.Sync;
using TodoApi.Data;
using TodoApi.Data.Entities;

namespace TodoApi.Infrastructure.Persistence;

public interface ISyncMappingRepository
{
    Task<SyncMapping?> FindByLocalIdAsync(
        EntityType entityType,
        Guid localId,
        CancellationToken ct = default
    );
    Task<SyncMapping?> FindByExternalIdAsync(
        EntityType entityType,
        string externalId,
        CancellationToken ct = default
    );
    Task AddAsync(SyncMapping mapping, CancellationToken ct = default);
    void Remove(SyncMapping mapping);
}

public sealed class SyncMappingRepository(TodoContext context) : ISyncMappingRepository
{
    public async Task<SyncMapping?> FindByLocalIdAsync(
        EntityType entityType,
        Guid localId,
        CancellationToken ct = default
    )
    {
        return await context
            .SyncMapping.FirstOrDefaultAsync(
                m => m.EntityType == entityType && m.LocalId == localId,
                ct
            )
            .ConfigureAwait(false);
    }

    public async Task<SyncMapping?> FindByExternalIdAsync(
        EntityType entityType,
        string externalId,
        CancellationToken ct = default
    )
    {
        return await context
            .SyncMapping.FirstOrDefaultAsync(
                m => m.EntityType == entityType && m.ExternalId == externalId,
                ct
            )
            .ConfigureAwait(false);
    }

    public async Task AddAsync(SyncMapping mapping, CancellationToken ct = default)
    {
        await context.SyncMapping.AddAsync(mapping, ct).ConfigureAwait(false);
    }

    public void Remove(SyncMapping mapping)
    {
        context.SyncMapping.Remove(mapping);
    }
}
