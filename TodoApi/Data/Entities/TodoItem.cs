using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Data.Entities;

public class TodoItem : ISynchronizable, IDeletable
{
    private TodoItem()
    {
        Name = string.Empty;
    }

    internal TodoItem(string name, Guid todoListId)
    {
        Name = name;
        TodoListId = todoListId;
        IsSynchronized = false;
        IsDeleted = false;
    }

    public long Id { get; set; }

    public string Name { get; private set; }

    public bool IsComplete { get; private set; }

    public Guid TodoListId { get; private set; }

    public TodoList TodoList { get; private set; } = null!;

    public bool IsSynchronized { get; private set; }

    public DateTime? SynchronizedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    internal void Rename(string name) => Name = name;

    internal void Complete()
    {
        IsComplete = true;
        IsSynchronized = false;
    }

    public void Synchronized(DateTime synchronizedAt)
    {
        IsSynchronized = true;
        SynchronizedAt = synchronizedAt;
    }

    public void MarkAsDeleted(DateTime deletedAt)
    {
        IsDeleted = true;
        IsSynchronized = false;
    }
}
