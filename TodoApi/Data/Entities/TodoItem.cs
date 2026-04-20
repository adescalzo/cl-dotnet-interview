namespace TodoApi.Data.Entities;

public class TodoItem
{
    private TodoItem()
    {
        Name = string.Empty;
    }

    internal TodoItem(string name, Guid todoListId)
    {
        Name = name;
        TodoListId = todoListId;
    }

    public long Id { get; set; }

    public string Name { get; private set; }

    public bool IsComplete { get; private set; }

    public Guid TodoListId { get; private set; }

    public TodoList TodoList { get; private set; } = null!;

    internal void Rename(string name) => Name = name;

    internal void Complete() => IsComplete = true;
}
