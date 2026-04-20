using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TodoApi.Application.Queries.GetTodoLists;
using TodoApi.Tests.Builders;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Application.Queries.GetTodoLists;

public sealed class GetTodoListsHandlerTests : AsyncLifetimeBase
{
    private GetTodoListsHandler _handler = null!;

    protected override Task OnInitializeAsync()
    {
        _handler = new GetTodoListsHandler(
            new TodoListQueryRepository(Context),
            NullLogger<GetTodoListsHandler>.Instance);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Handle_WhenNoTodoListsSeeded_ShouldReturnEmptyResponse()
    {
        // Arrange
        var query = new GetTodoListsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.GetValue.TodoLists.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTodoListsSeeded_ShouldReturnAllMappedToSummaries()
    {
        // Arrange
        var first = new TodoListBuilder().WithName("Groceries").Build();
        var second = new TodoListBuilder().WithName("Chores").Build();
        Context.TodoList.Add(first);
        Context.TodoList.Add(second);
        await SaveChangesAsync();

        var item = new TodoItemBuilder()
            .WithName("Milk")
            .WithIsComplete(true)
            .WithTodoListId(first.Id)
            .Build();
        Context.TodoItem.Add(item);
        await SaveChangesAsync();

        var query = new GetTodoListsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.GetValue.TodoLists.Should().HaveCount(2);
        result.GetValue.TodoLists.Should().Contain(s => s.Id == first.Id && s.Name == "Groceries");
        result.GetValue.TodoLists.Should().Contain(s => s.Id == second.Id && s.Name == "Chores");

        var groceries = result.GetValue.TodoLists.Single(s => s.Id == first.Id);
        groceries.Items.Should().HaveCount(1);
        groceries.Items[0].Id.Should().Be(item.Id);
        groceries.Items[0].Name.Should().Be("Milk");
        groceries.Items[0].IsComplete.Should().BeTrue();

        var chores = result.GetValue.TodoLists.Single(s => s.Id == second.Id);
        chores.Items.Should().BeEmpty();
    }
}
