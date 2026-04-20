using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TodoApi.Application.Commands.CompleteTodoItem;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Tests.Builders;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Application.Commands.CompleteTodoItem;

public sealed class CompleteTodoItemHandlerTests : AsyncLifetimeBase
{
    private CompleteTodoItemHandler _handler = null!;
    private TodoList _seededList = null!;
    private TodoItem _seededItem = null!;

    protected override async Task OnInitializeAsync()
    {
        _seededList = new TodoListBuilder().WithName("Groceries").Build();
        Context.TodoList.Add(_seededList);
        await SaveChangesAsync().ConfigureAwait(false);

        _seededItem = new TodoItemBuilder()
            .WithName("Milk")
            .WithIsComplete(false)
            .WithTodoListId(_seededList.Id)
            .Build();
        Context.TodoItem.Add(_seededItem);
        await SaveChangesAsync().ConfigureAwait(false);

        _handler = new CompleteTodoItemHandler(
            new TodoListCommandRepository(Context),
            Clock,
            NullLogger<CompleteTodoItemHandler>.Instance
        );
    }

    [Fact]
    public async Task Handle_WhenItemExists_ShouldMarkCompleteAndReturnResponse()
    {
        // Arrange
        var command = new CompleteTodoItemCommand(_seededList.Id, _seededItem.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        await SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.GetValue.Id.Should().Be(_seededItem.Id);
        result.GetValue.IsComplete.Should().BeTrue();

        var persistedItem = await Context
            .TodoItem.AsNoTracking()
            .SingleAsync(x => x.Id == _seededItem.Id);
        persistedItem.IsComplete.Should().BeTrue();

        var persistedList = await Context
            .TodoList.AsNoTracking()
            .SingleAsync(x => x.Id == _seededList.Id);
        persistedList.UpdatedAt.Should().Be(UtcNow);
    }

    [Fact]
    public async Task Handle_WhenTodoListDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var command = new CompleteTodoItemCommand(Guid.NewGuid(), _seededItem.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Definition.Should().Be(ErrorDefinition.NotFound);

        var persisted = await Context
            .TodoItem.AsNoTracking()
            .SingleAsync(x => x.Id == _seededItem.Id);
        persisted.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenItemDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var command = new CompleteTodoItemCommand(_seededList.Id, 99999);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Definition.Should().Be(ErrorDefinition.NotFound);
    }
}
