using TodoApi.Application.Sync;
using TodoApi.Infrastructure.Extensions;

namespace TodoApi.Data.Entities;

public class SyncMapping
{
    private SyncMapping()
    {
        ExternalId = string.Empty;
    }

    public SyncMapping(
        EntityType entityType,
        Guid localId,
        string externalId,
        DateTime externalUpdatedAt
    )
    {
        EntityType = entityType;
        LocalId = localId;
        ExternalId = externalId;
        ExternalUpdatedAt = externalUpdatedAt;
        LastSyncedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; } = GuidV7.NewGuid();

    public EntityType EntityType { get; private set; }

    public Guid LocalId { get; private set; }

    public string ExternalId { get; private set; }

    public DateTime ExternalUpdatedAt { get; private set; }

    public DateTime LastSyncedAt { get; private set; }

    public void UpdateSync(string externalId, DateTime externalUpdatedAt)
    {
        ExternalId = externalId;
        ExternalUpdatedAt = externalUpdatedAt;
        LastSyncedAt = DateTime.UtcNow;
    }
}
