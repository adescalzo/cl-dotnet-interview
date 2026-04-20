using FluentAssertions;
using TodoApi.Application.Queries.GetTodoList;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Tests.Builders;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Application.Queries.GetTodoList;

public sealed class GetTodoListHandlerTests : AsyncLifetimeBase
{
    private GetTodoListHandler _handler = null!;
    private TodoList _seeded = null!;
    private TodoItem _seededItem = null!;

    protected override async Task OnInitializeAsync()
    {
        _seeded = new TodoListBuilder().WithName("Groceries").Build();
        Context.TodoList.Add(_seeded);
        await SaveChangesAsync().ConfigureAwait(false);

        _seededItem = new TodoItemBuilder()
            .WithName("Milk")
            .WithIsComplete(false)
            .WithTodoListId(_seeded.Id)
            .Build();
        Context.TodoItem.Add(_seededItem);
        await SaveChangesAsync().ConfigureAwait(false);

        _handler = new GetTodoListHandler(new TodoListQueryRepository(Context));
    }

    [Fact]
    public async Task Handle_WhenTodoListExists_ShouldReturnMappedResponse()
    {
        // Arrange
        var query = new GetTodoListQuery(_seeded.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.GetValue.Id.Should().Be(_seeded.Id);
        result.GetValue.Name.Should().Be("Groceries");
        result.GetValue.CreatedAt.Should().Be(_seeded.CreatedAt);
        result.GetValue.Items.Should().HaveCount(1);
        result.GetValue.Items[0].Id.Should().Be(_seededItem.Id);
        result.GetValue.Items[0].Name.Should().Be("Milk");
        result.GetValue.Items[0].IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenTodoListDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var query = new GetTodoListQuery(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Definition.Should().Be(ErrorDefinition.NotFound);
    }
}
