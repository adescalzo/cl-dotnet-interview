namespace TodoApi.Data.Entities;

public class TodoList
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public ICollection<TodoItem> Items { get; } = [];
}
