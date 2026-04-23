using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TodoApi.Application.Commands.DeleteTodoList;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Extensions;
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
            new SyncEventCommandRepository(Context),
            Clock,
            NullLogger<DeleteTodoListHandler>.Instance
        );
    }

    [Fact]
    public async Task Handle_WhenTodoListExists_ShouldMarkAsDeletedAndReturnSuccess()
    {
        var command = new DeleteTodoListCommand(_seeded.Id);

        var result = await _handler.Handle(command, CancellationToken.None).ConfigureAwait(false);
        await SaveChangesAsync().ConfigureAwait(false);

        result.IsSuccess.Should().BeTrue();

        var persisted = await Context
            .TodoList.IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(x => x.Id == _seeded.Id)
            .ConfigureAwait(false);
        persisted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTodoListHasItems_ShouldMarkItemsAsDeletedToo()
    {
        var item = _seeded.AddItem("Buy milk", 1, UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        var command = new DeleteTodoListCommand(_seeded.Id);
        await _handler.Handle(command, CancellationToken.None).ConfigureAwait(false);
        await SaveChangesAsync().ConfigureAwait(false);

        var persistedItem = await Context
            .TodoItem.IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(x => x.Id == item.Id)
            .ConfigureAwait(false);
        persistedItem.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTodoListDoesNotExist_ShouldReturnNotFound()
    {
        var command = new DeleteTodoListCommand(Guid.NewGuid());
        var result = await _handler.Handle(command, CancellationToken.None).ConfigureAwait(false);

        result.IsFailure.Should().BeTrue();
        result.Error.Definition.Should().Be(ErrorDefinition.NotFound);

        var seededStillThere = await Context
            .TodoList.IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(x => x.Id == _seeded.Id)
            .ConfigureAwait(false);
        seededStillThere.Should().BeTrue();
    }
}
