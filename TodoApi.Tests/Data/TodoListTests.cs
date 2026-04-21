using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data.Entities;
using TodoApi.Tests.Builders;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Data;

public sealed class TodoListTests : AsyncLifetimeBase
{
    [Fact]
    public async Task Add_WhenSaved_ShouldPersistAllProperties()
    {
        // Arrange
        var list = new TodoListBuilder().WithName("Groceries").WithCreatedAt(UtcNow).Build();

        // Act
        Context.TodoList.Add(list);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert
        var persisted = await Context.TodoList.AsNoTracking().SingleAsync(x => x.Id == list.Id);
        persisted.Name.Should().Be("Groceries");
        persisted.CreatedAt.Should().Be(UtcNow);
        persisted.UpdatedAt.Should().BeNull();
        persisted.IsDeleted.Should().BeFalse();
        persisted.IsSynchronized.Should().BeFalse();
    }

    [Fact]
    public async Task Update_WhenSaved_ShouldPersistNewNameAndTimestamp()
    {
        // Arrange
        var list = new TodoListBuilder().WithName("Groceries").WithCreatedAt(UtcNow).Build();
        Context.TodoList.Add(list);
        await SaveChangesAsync().ConfigureAwait(false);

        // Act
        list.Update("Chores", UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert
        var persisted = await Context.TodoList.AsNoTracking().SingleAsync(x => x.Id == list.Id).ConfigureAwait(false);
        persisted.Name.Should().Be("Chores");
        persisted.UpdatedAt.Should().Be(UtcNow);
        persisted.IsSynchronized.Should().BeFalse();
    }

    // --- Logic Delete ---

    [Fact]
    public async Task MarkAsDeleted_WhenSaved_ShouldPersistDeletedFlag()
    {
        // Arrange
        var list = new TodoListBuilder().WithCreatedAt(UtcNow).Build();
        Context.TodoList.Add(list);
        await SaveChangesAsync().ConfigureAwait(false);

        // Act
        list.MarkAsDeleted(UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert — bypass global query filter to confirm the row still exists and is flagged
        var persisted = await Context
            .TodoList.IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(x => x.Id == list.Id)
            .ConfigureAwait(false);
        persisted.IsDeleted.Should().BeTrue();
        persisted.UpdatedAt.Should().Be(UtcNow);
        persisted.IsSynchronized.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAsDeleted_WhenListHasItems_ShouldPersistDeletedFlagOnAllItems()
    {
        // Arrange
        var list = new TodoListBuilder().WithCreatedAt(UtcNow).Build();
        Context.TodoList.Add(list);
        await SaveChangesAsync().ConfigureAwait(false);

        list.AddItem("Milk", UtcNow);
        list.AddItem("Eggs", UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Act
        list.MarkAsDeleted(UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert
        var persistedItems = await Context
            .TodoItem.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TodoListId == list.Id)
            .ToListAsync()
            .ConfigureAwait(false);
        persistedItems.Should().HaveCount(2);
        persistedItems.Should().AllSatisfy(i => i.IsDeleted.Should().BeTrue());
    }

    [Fact]
    public async Task AddItem_WhenSaved_ShouldPersistItemWithCorrectState()
    {
        // Arrange
        var list = new TodoListBuilder().WithCreatedAt(UtcNow).Build();
        Context.TodoList.Add(list);
        await SaveChangesAsync().ConfigureAwait(false);

        // Act
        list.AddItem("Milk", UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert
        var persistedItem = await Context
            .TodoItem.AsNoTracking()
            .SingleAsync(x => x.TodoListId == list.Id)
            .ConfigureAwait(false);
        persistedItem.Name.Should().Be("Milk");
        persistedItem.IsComplete.Should().BeFalse();
        persistedItem.IsDeleted.Should().BeFalse();
        persistedItem.TodoListId.Should().Be(list.Id);

        var persistedList = await Context
            .TodoList
            .AsNoTracking()
            .SingleAsync(x => x.Id == list.Id)
            .ConfigureAwait(false);
        persistedList.UpdatedAt.Should().Be(UtcNow);
    }

    [Fact]
    public async Task UpdateItem_WhenItemExists_ShouldPersistRenamedItem()
    {
        // Arrange
        var list = new TodoListBuilder().WithCreatedAt(UtcNow).Build();
        Context.TodoList.Add(list);
        await SaveChangesAsync().ConfigureAwait(false);

        var item = list.AddItem("Milk", UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Act
        list.UpdateItem(item.Id, "Oat Milk", UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert
        var persistedItem =
            await Context.TodoItem.AsNoTracking().SingleAsync(x => x.Id == item.Id).ConfigureAwait(false);
        persistedItem.Name.Should().Be("Oat Milk");

        var persistedList =
            await Context.TodoList.AsNoTracking().SingleAsync(x => x.Id == list.Id).ConfigureAwait(false);
        persistedList.UpdatedAt.Should().Be(UtcNow);
    }

    [Fact]
    public async Task UpdateItem_WhenItemDoesNotExist_ShouldNotModifyList()
    {
        // Arrange
        var list = new TodoListBuilder().WithName("Groceries").WithCreatedAt(UtcNow).Build();
        Context.TodoList.Add(list);
        await SaveChangesAsync().ConfigureAwait(false);

        // Act
        var updated = list.UpdateItem(999, "Milk", UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert
        updated.Should().BeNull();

        var persisted = await Context.TodoList.AsNoTracking().SingleAsync(x => x.Id == list.Id).ConfigureAwait(false);
        persisted.Name.Should().Be("Groceries");
        persisted.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task RemoveItem_WhenItemExists_ShouldPersistDeletedFlagOnItem()
    {
        // Arrange
        var list = new TodoListBuilder().WithCreatedAt(UtcNow).Build();
        Context.TodoList.Add(list);
        await SaveChangesAsync().ConfigureAwait(false);

        var item = list.AddItem("Milk", UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Act
        list.RemoveItem(item.Id, UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert
        var persistedItem = await Context
            .TodoItem.IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(x => x.Id == item.Id)
            .ConfigureAwait(false);
        persistedItem.IsDeleted.Should().BeTrue();

        var persistedList =
            await Context.TodoList.AsNoTracking().SingleAsync(x => x.Id == list.Id).ConfigureAwait(false);
        persistedList.UpdatedAt.Should().Be(UtcNow);
    }

    [Fact]
    public async Task RemoveItem_WhenItemDoesNotExist_ShouldNotModifyList()
    {
        // Arrange
        var list = new TodoListBuilder().WithCreatedAt(UtcNow).Build();
        Context.TodoList.Add(list);
        await SaveChangesAsync().ConfigureAwait(false);

        // Act
        var removed = list.RemoveItem(999, UtcNow);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert
        removed.Should().BeFalse();

        var persisted = await Context.TodoList.AsNoTracking().SingleAsync(x => x.Id == list.Id).ConfigureAwait(false);
        persisted.UpdatedAt.Should().BeNull();
    }
}
