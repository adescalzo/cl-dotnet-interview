using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Data.Entities;

public class TodoList : Entity, IAuditable, ISynchronizable, IDeletable
{
    private readonly List<TodoItem> _items = [];

    private TodoList()
    {
        Name = string.Empty;
    }

    public TodoList(string name, DateTime createdAt)
    {
        Name = name;
        CreatedAt = createdAt;
        IsSynchronized = false;
        IsDeleted = false;
    }

    public string Name { get; private set; }

    public IReadOnlyCollection<TodoItem> Items => _items;

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public bool IsSynchronized { get; private set; }

    public DateTime? SynchronizedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public string? ExternalId { get; private set; }

    public void Update(string name, DateTime updatedAt)
    {
        Name = name;
        UpdatedAt = updatedAt;
        IsSynchronized = false;
    }

    public TodoItem AddItem(string name, int order, DateTime eventAt)
    {
        var item = new TodoItem(name, Id, order, eventAt);

        _items.Add(item);
        UpdatedAt = eventAt;
        IsSynchronized = false;

        return item;
    }

    public TodoItem? UpdateItem(Guid itemId, string name, DateTime updatedAt)
    {
        var item = _items.Find(i => i.Id == itemId);
        if (item is null)
        {
            return null;
        }

        item.Rename(name);
        UpdatedAt = updatedAt;
        IsSynchronized = false;

        return item;
    }

    public TodoItem? CompleteItem(Guid itemId, DateTime updatedAt)
    {
        var item = _items.Find(i => i.Id == itemId);
        if (item is null)
        {
            return null;
        }

        item.Complete(updatedAt);
        UpdatedAt = updatedAt;
        IsSynchronized = false;

        return item;
    }

    public int CompleteAllItems(DateTime now)
    {
        var count = 0;
        foreach (var item in Items.Where(i => !i.IsComplete))
        {
            item.Complete(now);
            count++;
        }

        return count;
    }

    public bool RemoveItem(Guid itemId, DateTime updatedAt)
    {
        var item = _items.Find(i => i.Id == itemId);
        if (item is null)
        {
            return false;
        }

        item.MarkAsDeleted(updatedAt);
        UpdatedAt = updatedAt;
        IsSynchronized = false;

        return true;
    }

    public void Synchronized(DateTime synchronizedAt)
    {
        IsSynchronized = true;
        SynchronizedAt = synchronizedAt;
    }

    public void MarkAsDeleted(DateTime deletedAt)
    {
        IsDeleted = true;
        UpdatedAt = deletedAt;
        IsSynchronized = false;

        foreach (var item in _items)
        {
            item.MarkAsDeleted(deletedAt);
        }
    }

    public void LinkExternal(string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ExternalId = externalId;
    }
}
