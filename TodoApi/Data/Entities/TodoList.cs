using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Data.Entities;

public class TodoList : Entity, IAuditable, ISynchronizable
{
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

    public ICollection<TodoItem> Items { get; } = [];

    public DateTime CreatedAt { get; }

    public DateTime? UpdatedAt { get; private set; }

    public bool IsSynchronized { get; private set; }

    public DateTime? SynchronizedAt { get; private set; }

    public void Update(string name, DateTime updatedAt)
    {
        Name = name;
        UpdatedAt = updatedAt;
    }

    public void AddItem(TodoItem item, DateTime updatedAt)
    {
        Items.Add(item);
        UpdatedAt = updatedAt;
    }

    public void RemoveItem(TodoItem item, DateTime upDateTime)
    {
        Items.Remove(item);
        UpdatedAt = upDateTime;
    }

    public void Synchronized(DateTime synchronizedAt)
    {
        IsSynchronized = true;
        SynchronizedAt = synchronizedAt;
    }
}
