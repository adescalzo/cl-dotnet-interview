using TodoApi.Infrastructure.Extensions;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Data.Entities;

public class TodoItem : ISynchronizable, IDeletable
{
    private TodoItem()
    {
        Name = string.Empty;
    }

    internal TodoItem(string name, Guid todoListId, int order, DateTime createdAt)
    {
        Id = GuidV7.NewGuid();
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

    public DateTime? CompletedAt { get; private set; }

    public Guid TodoListId { get; private set; }

    public TodoList TodoList { get; private set; } = null!;

    public bool IsSynchronized { get; private set; }

    public DateTime? SynchronizedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public string? ExternalId { get; private set; }

    internal void Rename(string name) => Name = name;

    internal void Complete(DateTime now)
    {
        IsComplete = true;
        CompletedAt = now;
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

    public void LinkExternal(string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ExternalId = externalId;
    }
}
