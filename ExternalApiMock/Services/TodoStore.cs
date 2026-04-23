using ExternalApiMock.Models;

namespace ExternalApiMock.Services;

public sealed class TodoStore
{
    private readonly List<TodoList> _lists = [];
    private readonly object _lock = new();

    public List<TodoList> All
    {
        get
        {
            lock (_lock)
                return [.. _lists];
        }
    }

    public TodoList? FindList(string id)
    {
        lock (_lock)
            return _lists.FirstOrDefault(l => l.Id == id || l.SourceId == id);
    }

    public void Add(TodoList list)
    {
        lock (_lock)
            _lists.Add(list);
    }

    public bool RemoveList(string id)
    {
        lock (_lock)
            return _lists.RemoveAll(l => l.Id == id || l.SourceId == id) > 0;
    }

    public void Seed()
    {
        var now = DateTime.UtcNow;

        var list1 = new TodoList(
            "ext-list-1",
            null,
            "Weekly groceries",
            now,
            now,
            [
                new TodoItem("ext-item-1", null, "Milk", false, now, now),
                new TodoItem("ext-item-2", null, "Bread", false, now, now),
            ]
        );

        var list2 = new TodoList(
            "ext-list-2",
            null,
            "Home tasks",
            now,
            now,
            [new TodoItem("ext-item-3", null, "Fix kitchen sink", true, now, now)]
        );

        lock (_lock)
        {
            _lists.Add(list1);
            _lists.Add(list2);
        }
    }
}
