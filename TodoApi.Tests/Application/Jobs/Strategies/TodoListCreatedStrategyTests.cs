using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Dtos;
using TodoApi.Application.Jobs.Strategies;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Application.Jobs.Strategies;

public sealed class TodoListCreatedStrategyTests : AsyncLifetimeBase
{
    private SyncMappingCommandRepository _mappings = null!;
    private IExternalTodoApiClient _client = null!;
    private TodoListCreatedStrategy _strategy = null!;

    protected override Task OnInitializeAsync()
    {
        _mappings = new SyncMappingCommandRepository(Context);
        _client = Substitute.For<IExternalTodoApiClient>();
        _strategy = new TodoListCreatedStrategy(_client, _mappings);

        return Task.CompletedTask;
    }

    [Fact]
    public void CanHandle_WhenTodoListCreated_ShouldReturnTrue()
    {
        var evt = new SyncEvent(EntityType.TodoList, Guid.NewGuid(), EventType.Created, "{}");
        _strategy.CanHandle(evt).Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WhenTodoListUpdated_ShouldReturnFalse()
    {
        var evt = new SyncEvent(EntityType.TodoList, Guid.NewGuid(), EventType.Updated, "{}");
        _strategy.CanHandle(evt).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoMappingExists_ShouldCallApiAndCreateMapping()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var payload = $"{{\"Id\":\"{listId}\",\"Name\":\"Groceries\"}}";
        var evt = new SyncEvent(EntityType.TodoList, listId, EventType.Created, payload);

        var externalUpdatedAt = DateTime.UtcNow;
        _client
            .CreateTodoListAsync(
                Arg.Any<CreateExternalTodoListRequest>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ExternalTodoList("ext-123", "Groceries", externalUpdatedAt, []));

        // Act
        await _strategy.ExecuteAsync(evt, CancellationToken.None);
        await SaveChangesAsync();

        // Assert
        await _client
            .Received(1)
            .CreateTodoListAsync(
                Arg.Is<CreateExternalTodoListRequest>(r => r.Name == "Groceries"),
                Arg.Any<CancellationToken>()
            )
            .ConfigureAwait(false);

        var mapping = await Context
            .SyncMapping.SingleAsync(m => m.LocalId == listId)
            .ConfigureAwait(false);

        mapping.ExternalId.Should().Be("ext-123");
        mapping.EntityType.Should().Be(EntityType.TodoList);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMappingAlreadyExists_ShouldSkipApiCall()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var existing = new SyncMapping(
            EntityType.TodoList,
            listId,
            "ext-existing",
            DateTime.UtcNow
        );
        await _mappings.AddAsync(existing).ConfigureAwait(false);
        await SaveChangesAsync().ConfigureAwait(false);

        var payload = $"{{\"Id\":\"{listId}\",\"Name\":\"Groceries\"}}";
        var evt = new SyncEvent(EntityType.TodoList, listId, EventType.Created, payload);

        // Act
        await _strategy.ExecuteAsync(evt, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await _client
            .DidNotReceive()
            .CreateTodoListAsync(
                Arg.Any<CreateExternalTodoListRequest>(),
                Arg.Any<CancellationToken>()
            )
            .ConfigureAwait(false);
    }
}
