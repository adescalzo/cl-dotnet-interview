using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TodoApi.Application.Commands.UpdateTodoList;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Tests.Builders;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Application.Commands.UpdateTodoList;

public sealed class UpdateTodoListHandlerTests : AsyncLifetimeBase
{
    private UpdateTodoListHandler _handler = null!;
    private TodoList _seeded = null!;

    protected override async Task OnInitializeAsync()
    {
        _seeded = new TodoListBuilder().WithName("Original").Build();
        Context.TodoList.Add(_seeded);
        await SaveChangesAsync().ConfigureAwait(false);

        _handler = new UpdateTodoListHandler(
            new TodoListCommandRepository(Context),
            new SyncEventCommandRepository(Context),
            Clock,
            NullLogger<UpdateTodoListHandler>.Instance
        );
    }

    [Fact]
    public async Task Handle_WhenTodoListExists_ShouldUpdateNameAndReturnResponse()
    {
        // Arrange
        var command = new UpdateTodoListCommand(_seeded.Id, "Renamed");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None).ConfigureAwait(false);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.GetValue.Id.Should().Be(_seeded.Id);
        result.GetValue.Name.Should().Be("Renamed");
        result.GetValue.UpdatedAt.Should().Be(UtcNow);

        var persisted = await Context
            .TodoList.AsNoTracking()
            .SingleAsync(x => x.Id == _seeded.Id)
            .ConfigureAwait(false);
        persisted.Name.Should().Be("Renamed");
        persisted.UpdatedAt.Should().Be(UtcNow);
    }

    [Fact]
    public async Task Handle_WhenTodoListDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var missingId = Guid.NewGuid();
        var command = new UpdateTodoListCommand(missingId, "Renamed");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Definition.Should().Be(ErrorDefinition.NotFound);

        var persisted = await Context.TodoList.AsNoTracking().SingleAsync().ConfigureAwait(false);
        persisted.Name.Should().Be("Original");
        persisted.UpdatedAt.Should().BeNull();
    }
}
