namespace TodoApi.Infrastructure.Persistence;

/// <summary>
/// Interface for entities that support audit tracking.
/// Tracks who created/updated the entity and when.
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// When this entity was created (UTC).
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// When this entity was last updated (UTC).
    /// </summary>
    DateTime? UpdatedAt { get; }
}
