using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TodoApi.Application.Commands.AddTodoItem;
using TodoApi.Application.Services;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Tests.Builders;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Application.Commands.AddTodoItem;

public sealed class AddTodoItemHandlerTests : AsyncLifetimeBase
{
    private AddTodoItemHandler _handler = null!;
    private TodoList _seeded = null!;

    protected override async Task OnInitializeAsync()
    {
        _seeded = new TodoListBuilder().WithName("Groceries").Build();
        Context.TodoList.Add(_seeded);
        await SaveChangesAsync().ConfigureAwait(false);

        _handler = new AddTodoItemHandler(
            new TodoListCommandRepository(Context),
            new SyncEventCommandRepository(Context),
            Substitute.For<IBulkOperationTracker>(),
            Clock,
            NullLogger<AddTodoItemHandler>.Instance
        );
    }

    [Fact]
    public async Task Handle_WhenTodoListExists_ShouldStageItemAndReturnResponse()
    {
        // Arrange
        var command = new AddTodoItemCommand(_seeded.Id, "Milk", 1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.GetValue.TodoListId.Should().Be(_seeded.Id);
        result.GetValue.Name.Should().Be("Milk");
        result.GetValue.IsComplete.Should().BeFalse();

        var persisted = await Context
            .TodoItem.AsNoTracking()
            .SingleAsync(x => x.TodoListId == _seeded.Id)
            .ConfigureAwait(false);
        persisted.Name.Should().Be("Milk");
        persisted.IsComplete.Should().BeFalse();

        var persistedList = await Context
            .TodoList.AsNoTracking()
            .SingleAsync(x => x.Id == _seeded.Id)
            .ConfigureAwait(false);
        persistedList.UpdatedAt.Should().Be(UtcNow);
    }

    [Fact]
    public async Task Handle_WhenTodoListDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var command = new AddTodoItemCommand(Guid.NewGuid(), "Milk", 1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Definition.Should().Be(ErrorDefinition.NotFound);

        var persistedItems = await Context
            .TodoItem.AsNoTracking()
            .CountAsync()
            .ConfigureAwait(false);
        persistedItems.Should().Be(0);
    }
}
