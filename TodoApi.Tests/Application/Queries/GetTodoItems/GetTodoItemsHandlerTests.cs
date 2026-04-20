using FluentAssertions;
using TodoApi.Application.Queries.GetTodoItems;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Tests.Builders;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Application.Queries.GetTodoItems;

public sealed class GetTodoItemsHandlerTests : AsyncLifetimeBase
{
    private GetTodoItemsHandler _handler = null!;
    private TodoList _seededList = null!;

    protected override async Task OnInitializeAsync()
    {
        _seededList = new TodoListBuilder().WithName("Groceries").Build();
        Context.TodoList.Add(_seededList);
        await SaveChangesAsync().ConfigureAwait(false);

        _handler = new GetTodoItemsHandler(new TodoListQueryRepository(Context));
    }

    [Fact]
    public async Task Handle_WhenTodoListHasItems_ShouldReturnMappedItems()
    {
        // Arrange
        var item1 = new TodoItemBuilder()
            .WithName("Milk")
            .WithIsComplete(false)
            .WithTodoListId(_seededList.Id)
            .Build();
        var item2 = new TodoItemBuilder()
            .WithName("Bread")
            .WithIsComplete(true)
            .WithTodoListId(_seededList.Id)
            .Build();
        Context.TodoItem.Add(item1);
        Context.TodoItem.Add(item2);
        await SaveChangesAsync();

        var query = new GetTodoItemsQuery(_seededList.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.GetValue.TodoListId.Should().Be(_seededList.Id);
        result.GetValue.Items.Should().HaveCount(2);
        result.GetValue.Items.Should().Contain(i => i.Name == "Milk" && !i.IsComplete);
        result.GetValue.Items.Should().Contain(i => i.Name == "Bread" && i.IsComplete);
    }

    [Fact]
    public async Task Handle_WhenTodoListHasNoItems_ShouldReturnEmptyList()
    {
        // Arrange
        var query = new GetTodoItemsQuery(_seededList.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.GetValue.TodoListId.Should().Be(_seededList.Id);
        result.GetValue.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTodoListDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var query = new GetTodoItemsQuery(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Definition.Should().Be(ErrorDefinition.NotFound);
    }
}
