namespace ExternalApiMock.Models;

public sealed class TodoItem(
    string id,
    string? sourceId,
    string description,
    bool completed,
    DateTime createdAt,
    DateTime updatedAt
)
{
    public string Id { get; } = id;
    public string? SourceId { get; } = sourceId;
    public string Description { get; set; } = description;
    public bool Completed { get; set; } = completed;
    public DateTime CreatedAt { get; } = createdAt;
    public DateTime UpdatedAt { get; set; } = updatedAt;
}
