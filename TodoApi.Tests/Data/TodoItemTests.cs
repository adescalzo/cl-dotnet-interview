using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data.Entities;
using TodoApi.Tests.Builders;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Data;

public sealed class TodoItemTests : AsyncLifetimeBase
{
    private TodoList _list = null!;

    protected override async Task OnInitializeAsync()
    {
        _list = new TodoListBuilder().WithCreatedAt(UtcNow).Build();
        Context.TodoList.Add(_list);
        await SaveChangesAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task AddItem_WhenSaved_ShouldPersistItemWithCorrectState()
    {
        // Act
        _list.AddItem("Milk", 1, UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert
        var persisted = await Context
            .TodoItem.AsNoTracking()
            .SingleAsync(x => x.TodoListId == _list.Id)
            .ConfigureAwait(false);
        persisted.Name.Should().Be("Milk");
        persisted.IsComplete.Should().BeFalse();
        persisted.IsDeleted.Should().BeFalse();
        persisted.IsSynchronized.Should().BeFalse();
        persisted.TodoListId.Should().Be(_list.Id);
    }

    [Fact]
    public async Task UpdateItem_WhenSaved_ShouldPersistRenamedItem()
    {
        // Arrange
        var item = _list.AddItem("Milk", 1, UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Act
        _list.UpdateItem(item.Id, "Oat Milk", UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert
        var persisted = await Context
            .TodoItem.AsNoTracking()
            .SingleAsync(x => x.Id == item.Id)
            .ConfigureAwait(false);
        persisted.Name.Should().Be("Oat Milk");
    }

    [Fact]
    public async Task RemoveItem_WhenSaved_ShouldPersistDeletedFlagOnItem()
    {
        // Arrange
        var item = _list.AddItem("Milk", 1, UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Act
        _list.RemoveItem(item.Id, UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert — bypass global query filter to confirm the row is flagged, not gone
        var persisted = await Context
            .TodoItem.IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(x => x.Id == item.Id)
            .ConfigureAwait(false);
        persisted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsDeleted_WhenSaved_ShouldPersistDeletedFlagOnItem()
    {
        // Arrange
        var item = new TodoItemBuilder().WithName("Milk").WithTodoListId(_list.Id).Build();
        Context.TodoItem.Add(item);
        await SaveChangesAsync().ConfigureAwait(false);

        // Act
        item.MarkAsDeleted(UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert
        var persisted = await Context
            .TodoItem.IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(x => x.Id == item.Id)
            .ConfigureAwait(false);
        persisted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void LinkExternal_WhenCalled_ShouldSetExternalId()
    {
        var item = new TodoItemBuilder().WithTodoListId(_list.Id).Build();

        item.LinkExternal("ext-item-1");

        item.ExternalId.Should().Be("ext-item-1");
    }
}
