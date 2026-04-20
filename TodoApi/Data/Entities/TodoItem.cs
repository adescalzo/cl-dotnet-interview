using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Data.Entities;

public class TodoItem
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public bool IsComplete { get; set; }
    public long TodoListId { get; set; }
    public TodoList TodoList { get; set; } = null!;
}
