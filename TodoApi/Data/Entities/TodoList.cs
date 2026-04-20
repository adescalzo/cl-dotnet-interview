using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Data.Entities;

public class TodoList : Entity, IAuditable, ISynchronizable
{
    private readonly List<TodoItem> _items = [];

    private TodoList()
    {
        Name = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public TodoList(string name, DateTime createdAt)
    {
        Name = name;
        CreatedAt = createdAt;
    }

    public string Name { get; private set; }

    public IReadOnlyCollection<TodoItem> Items => _items;

    public DateTime CreatedAt { get; }

    public DateTime? UpdatedAt { get; private set; }

    public bool IsSynchronized { get; private set; }

    public DateTime? SynchronizedAt { get; private set; }

    public void Update(string name, DateTime updatedAt)
    {
        Name = name;
        UpdatedAt = updatedAt;
    }

    public TodoItem AddItem(string name, DateTime updatedAt)
    {
        var item = new TodoItem(name, Id);
        _items.Add(item);
        UpdatedAt = updatedAt;
        return item;
    }

    public TodoItem? UpdateItem(long itemId, string name, DateTime updatedAt)
    {
        var item = _items.Find(i => i.Id == itemId);
        if (item is null)
        {
            return null;
        }

        item.Rename(name);
        UpdatedAt = updatedAt;
        return item;
    }

    public TodoItem? CompleteItem(long itemId, DateTime updatedAt)
    {
        var item = _items.Find(i => i.Id == itemId);
        if (item is null)
        {
            return null;
        }

        item.Complete();
        UpdatedAt = updatedAt;
        return item;
    }

    public bool RemoveItem(long itemId, DateTime updatedAt)
    {
        var item = _items.Find(i => i.Id == itemId);
        if (item is null)
        {
            return false;
        }

        _items.Remove(item);
        UpdatedAt = updatedAt;
        return true;
    }

    public void Synchronized(DateTime synchronizedAt)
    {
        IsSynchronized = true;
        SynchronizedAt = synchronizedAt;
    }
}
