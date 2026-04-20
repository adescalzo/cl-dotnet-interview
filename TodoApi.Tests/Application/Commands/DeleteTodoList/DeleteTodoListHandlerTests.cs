using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TodoApi.Application.Commands.DeleteTodoList;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Tests.Builders;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Application.Commands.DeleteTodoList;

public sealed class DeleteTodoListHandlerTests : AsyncLifetimeBase
{
    private DeleteTodoListHandler _handler = null!;
    private TodoList _seeded = null!;

    protected override async Task OnInitializeAsync()
    {
        _seeded = new TodoListBuilder().WithName("To be deleted").Build();
        Context.TodoList.Add(_seeded);
        await SaveChangesAsync().ConfigureAwait(false);

        _handler = new DeleteTodoListHandler(
            new TodoListCommandRepository(Context),
            NullLogger<DeleteTodoListHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WhenTodoListExists_ShouldRemoveRowAndReturnSuccess()
    {
        // Arrange
        var command = new DeleteTodoListCommand(_seeded.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        await SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();

        var exists = await Context.TodoList.AsNoTracking().AnyAsync(x => x.Id == _seeded.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenTodoListDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var missingId = Guid.NewGuid();
        var command = new DeleteTodoListCommand(missingId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Definition.Should().Be(ErrorDefinition.NotFound);

        var seededStillThere = await Context.TodoList.AsNoTracking().AnyAsync(x => x.Id == _seeded.Id);
        seededStillThere.Should().BeTrue();
    }
}
