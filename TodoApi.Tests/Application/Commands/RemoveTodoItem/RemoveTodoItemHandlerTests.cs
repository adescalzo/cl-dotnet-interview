using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TodoApi.Application.Commands.RemoveTodoItem;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Tests.Builders;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Application.Commands.RemoveTodoItem;

public sealed class RemoveTodoItemHandlerTests : AsyncLifetimeBase
{
    private RemoveTodoItemHandler _handler = null!;
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

        _handler = new RemoveTodoItemHandler(
            new TodoListCommandRepository(Context),
            Clock,
            NullLogger<RemoveTodoItemHandler>.Instance
        );
    }

    [Fact]
    public async Task Handle_WhenItemExists_ShouldRemoveAndReturnSuccess()
    {
        // Arrange
        var command = new RemoveTodoItemCommand(_seededList.Id, _seededItem.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        await SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();

        var exists = await Context.TodoItem.AsNoTracking().AnyAsync(x => x.Id == _seededItem.Id);
        exists.Should().BeFalse();

        var persistedList = await Context
            .TodoList.AsNoTracking()
            .SingleAsync(x => x.Id == _seededList.Id);
        persistedList.UpdatedAt.Should().Be(UtcNow);
    }

    [Fact]
    public async Task Handle_WhenTodoListDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var command = new RemoveTodoItemCommand(Guid.NewGuid(), _seededItem.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Definition.Should().Be(ErrorDefinition.NotFound);

        var exists = await Context.TodoItem.AsNoTracking().AnyAsync(x => x.Id == _seededItem.Id);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenItemDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var command = new RemoveTodoItemCommand(_seededList.Id, 99999);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Definition.Should().Be(ErrorDefinition.NotFound);

        var exists = await Context.TodoItem.AsNoTracking().AnyAsync(x => x.Id == _seededItem.Id);
        exists.Should().BeTrue();
    }
}
