using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Data.Entities;

public class TodoItem : ISynchronizable, IDeletable
{
    private TodoItem()
    {
        Name = string.Empty;
    }

    internal TodoItem(Guid id, string name, Guid todoListId, int order, DateTime createdAt)
    {
        Id = id;
        Name = name;
        TodoListId = todoListId;
        Order = order;
        CreatedAt = createdAt;
        IsSynchronized = false;
        IsDeleted = false;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public bool IsComplete { get; private set; }

    public int Order { get; private set; }

    public DateTime CreatedAt { get; private set; }

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
