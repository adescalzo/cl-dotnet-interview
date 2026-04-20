using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TodoApi.Application.Commands.UpdateTodoItem;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Tests.Builders;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Application.Commands.UpdateTodoItem;

public sealed class UpdateTodoItemHandlerTests : AsyncLifetimeBase
{
    private UpdateTodoItemHandler _handler = null!;
    private TodoList _seededList = null!;
    private TodoItem _seededItem = null!;

    protected override async Task OnInitializeAsync()
    {
        _seededList = new TodoListBuilder().WithName("Groceries").Build();
        Context.TodoList.Add(_seededList);
        await SaveChangesAsync().ConfigureAwait(false);

        _seededItem = new TodoItemBuilder()
            .WithName("Original")
            .WithIsComplete(false)
            .WithTodoListId(_seededList.Id)
            .Build();
        Context.TodoItem.Add(_seededItem);
        await SaveChangesAsync().ConfigureAwait(false);

        _handler = new UpdateTodoItemHandler(
            new TodoListCommandRepository(Context),
            Clock,
            NullLogger<UpdateTodoItemHandler>.Instance
        );
    }

    [Fact]
    public async Task Handle_WhenItemExists_ShouldUpdateNameAndReturnResponse()
    {
        // Arrange
        var command = new UpdateTodoItemCommand(_seededList.Id, _seededItem.Id, "Renamed");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        await SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.GetValue.Id.Should().Be(_seededItem.Id);
        result.GetValue.TodoListId.Should().Be(_seededList.Id);
        result.GetValue.Name.Should().Be("Renamed");

        var persistedItem = await Context
            .TodoItem.AsNoTracking()
            .SingleAsync(x => x.Id == _seededItem.Id);
        persistedItem.Name.Should().Be("Renamed");

        var persistedList = await Context
            .TodoList.AsNoTracking()
            .SingleAsync(x => x.Id == _seededList.Id);
        persistedList.UpdatedAt.Should().Be(UtcNow);
    }

    [Fact]
    public async Task Handle_WhenTodoListDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var command = new UpdateTodoItemCommand(Guid.NewGuid(), _seededItem.Id, "Renamed");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Definition.Should().Be(ErrorDefinition.NotFound);

        var persisted = await Context
            .TodoItem.AsNoTracking()
            .SingleAsync(x => x.Id == _seededItem.Id);
        persisted.Name.Should().Be("Original");
    }

    [Fact]
    public async Task Handle_WhenItemDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var command = new UpdateTodoItemCommand(_seededList.Id, 99999, "Renamed");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Definition.Should().Be(ErrorDefinition.NotFound);

        var persisted = await Context
            .TodoItem.AsNoTracking()
            .SingleAsync(x => x.Id == _seededItem.Id);
        persisted.Name.Should().Be("Original");
    }
}
