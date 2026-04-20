using System.ComponentModel.DataAnnotations;
using TodoApi.Infrastructure.Extensions;

namespace TodoApi.Infrastructure.Persistence;

/// <summary>
/// Base class for all domain entities.
/// Provides identity and domain event support.
/// Pure domain - no infrastructure dependencies.
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// Entity unique identifier.
    /// Uses Guid for globally unique IDs.
    /// </summary>
    public Guid Id { get; protected init; } = GuidV7.NewGuid();

    /// <summary>
    /// Concurrency token for optimistic concurrency control.
    /// </summary>
    [Timestamp] // Maps to SQL Server 'rowversion'
    public byte[] Version { get; set; } = [];
}
