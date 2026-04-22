namespace ExternalApiMock.Models;

public sealed class TodoList(
    string id,
    string? sourceId,
    string name,
    DateTime createdAt,
    DateTime updatedAt,
    List<TodoItem> items
)
{
    public string Id { get; } = id;
    public string? SourceId { get; } = sourceId;
    public string Name { get; set; } = name;
    public DateTime CreatedAt { get; } = createdAt;
    public DateTime UpdatedAt { get; set; } = updatedAt;
    public List<TodoItem> Items { get; } = items;
}