namespace TodoApi.Infrastructure.Persistence;

public interface IDeletable
{
    /// <summary>
    /// Indicates whether this entity is marked as deleted.
    /// </summary>
    bool IsDeleted { get; }

    void MarkAsDeleted(DateTime deletedAt);
}
