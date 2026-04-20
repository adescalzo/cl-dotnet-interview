using Bogus;
using TodoApi.Data.Entities;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Builders;

public sealed class TodoItemBuilder : IBuilder<TodoItem>
{
    private readonly Faker<TodoItemData> _faker;

    public TodoItemBuilder() : this(CreateDefault()) { }

    private TodoItemBuilder(Faker<TodoItemData> faker) => _faker = faker;

    public TodoItemBuilder WithName(string name) =>
        CloneWith(f => f.RuleFor(x => x.Name, name));

    public TodoItemBuilder WithIsComplete(bool isComplete) =>
        CloneWith(f => f.RuleFor(x => x.IsComplete, isComplete));

    public TodoItemBuilder WithTodoListId(Guid todoListId) =>
        CloneWith(f => f.RuleFor(x => x.TodoListId, todoListId));

    public TodoItem Build()
    {
        var data = _faker.Generate();
        return new TodoItem
        {
            Name = data.Name,
            IsComplete = data.IsComplete,
            TodoListId = data.TodoListId,
        };
    }

    public IEnumerable<TodoItem> BuildList(int count) =>
        Enumerable.Range(0, count).Select(_ => Build());

    private TodoItemBuilder CloneWith(Action<Faker<TodoItemData>> configure)
    {
        var clone = _faker.Clone();
        configure(clone);
        return new TodoItemBuilder(clone);
    }

    private static Faker<TodoItemData> CreateDefault() =>
        new Faker<TodoItemData>()
            .RuleFor(x => x.Name, f => f.Lorem.Sentence(2))
            .RuleFor(x => x.IsComplete, f => f.Random.Bool())
            .RuleFor(x => x.TodoListId, _ => Guid.NewGuid());

    private sealed class TodoItemData
    {
        public string Name { get; set; } = string.Empty;

        public bool IsComplete { get; set; } = true;

        public Guid TodoListId { get; set; } = Guid.NewGuid();
    }
}
