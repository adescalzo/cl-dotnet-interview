namespace TodoApi.Infrastructure.Persistence;

/// <summary>
/// Interface for entities that support audit tracking.
/// Tracks who created/updated the entity and when.
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// Who created this entity.
    /// </summary>
    string CreatedBy { get; }

    /// <summary>
    /// When this entity was created (UTC).
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// Who last updated this entity.
    /// </summary>
    string? UpdatedBy { get; }

    /// <summary>
    /// When this entity was last updated (UTC).
    /// </summary>
    DateTime? UpdatedAt { get; }
}
