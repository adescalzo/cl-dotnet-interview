using Bogus;
using TodoApi.Data.Entities;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Builders;

public sealed class TodoListBuilder : IBuilder<TodoList>
{
    private readonly Faker<TodoListData> _faker;

    public TodoListBuilder()
        : this(CreateDefault()) { }

    private TodoListBuilder(Faker<TodoListData> faker) => _faker = faker;

    public TodoListBuilder WithName(string name) => CloneWith(f => f.RuleFor(x => x.Name, name));

    public TodoListBuilder WithCreatedAt(DateTime createdAt) =>
        CloneWith(f => f.RuleFor(x => x.CreatedAt, createdAt));

    public TodoList Build()
    {
        var data = _faker.Generate();
        return new TodoList(data.Name, data.CreatedAt);
    }

    public IEnumerable<TodoList> BuildList(int count) =>
        Enumerable.Range(0, count).Select(_ => Build());

    private TodoListBuilder CloneWith(Action<Faker<TodoListData>> configure)
    {
        var clone = _faker.Clone();
        configure(clone);
        return new TodoListBuilder(clone);
    }

    private static Faker<TodoListData> CreateDefault() =>
        new Faker<TodoListData>()
            .RuleFor(x => x.Name, f => f.Lorem.Sentence(3))
            .RuleFor(x => x.CreatedAt, f => f.Date.RecentOffset().UtcDateTime);

    private sealed class TodoListData
    {
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
