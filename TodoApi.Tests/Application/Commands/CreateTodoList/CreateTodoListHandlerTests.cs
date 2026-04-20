using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TodoApi.Application.Commands.CreateTodoList;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Application.Commands.CreateTodoList;

public sealed class CreateTodoListHandlerTests : AsyncLifetimeBase
{
    private CreateTodoListHandler _handler = null!;

    protected override Task OnInitializeAsync()
    {
        _handler = new CreateTodoListHandler(
            new TodoListCommandRepository(Context),
            Clock,
            NullLogger<CreateTodoListHandler>.Instance
        );

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Handle_WhenCalled_ShouldPersistTodoListAndReturnResponse()
    {
        // Arrange
        var command = new CreateTodoListCommand("Groceries");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        await SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.GetValue.Name.Should().Be("Groceries");
        result.GetValue.CreatedAt.Should().Be(UtcNow);
        result.GetValue.Id.Should().NotBe(Guid.Empty);

        var persisted = await Context.TodoList.AsNoTracking().SingleAsync();
        persisted.Id.Should().Be(result.GetValue.Id);
        persisted.Name.Should().Be("Groceries");
    }
}
