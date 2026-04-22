using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Refit;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.Jobs.Strategies;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Application.Jobs.Strategies;

public sealed class TodoListDeletedStrategyTests : AsyncLifetimeBase
{
    private SyncMappingCommandRepository _mappings = null!;
    private IExternalTodoApiClient _client = null!;
    private TodoListDeletedStrategy _strategy = null!;

    protected override Task OnInitializeAsync()
    {
        _mappings = new SyncMappingCommandRepository(Context);
        _client = Substitute.For<IExternalTodoApiClient>();
        _strategy = new TodoListDeletedStrategy(_client, _mappings);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ExecuteAsync_WhenMappingExists_ShouldDeleteExternalAndRemoveMapping()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var mapping = new SyncMapping(EntityType.TodoList, listId, "ext-123", DateTime.UtcNow);
        await _mappings.AddAsync(mapping).ConfigureAwait(false);
        await SaveChangesAsync().ConfigureAwait(false);

        var payload = $"{{\"Id\":\"{listId}\"}}";
        var evt = new SyncEvent(EntityType.TodoList, listId, EventType.Deleted, payload);

        // Act
        await _strategy.ExecuteAsync(evt, CancellationToken.None).ConfigureAwait(false);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert
        await _client
            .Received(1)
            .DeleteTodoListAsync("ext-123", Arg.Any<CancellationToken>())
            .ConfigureAwait(false);

        var mappingGone = await Context
            .SyncMapping.AnyAsync(m => m.LocalId == listId)
            .ConfigureAwait(false);
        mappingGone.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenMappingDoesNotExist_ShouldSkip()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var payload = $"{{\"Id\":\"{listId}\"}}";
        var evt = new SyncEvent(EntityType.TodoList, listId, EventType.Deleted, payload);

        // Act
        await _strategy.ExecuteAsync(evt, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await _client
            .DidNotReceive()
            .DeleteTodoListAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExternalReturns404_ShouldStillRemoveMapping()
    {
        // Arrange
        var listId = Guid.NewGuid();
        var mapping = new SyncMapping(EntityType.TodoList, listId, "ext-gone", DateTime.UtcNow);
        await _mappings.AddAsync(mapping).ConfigureAwait(false);
        await SaveChangesAsync().ConfigureAwait(false);

        _client
            .DeleteTodoListAsync("ext-gone", Arg.Any<CancellationToken>())
            .ThrowsAsync(
                await ApiException.Create(
                    new HttpRequestMessage(),
                    HttpMethod.Delete,
                    new HttpResponseMessage(HttpStatusCode.NotFound),
                    new RefitSettings()
                )
            );

        var payload = $"{{\"Id\":\"{listId}\"}}";
        var evt = new SyncEvent(EntityType.TodoList, listId, EventType.Deleted, payload);

        // Act
        await _strategy.ExecuteAsync(evt, CancellationToken.None).ConfigureAwait(false);
        await SaveChangesAsync().ConfigureAwait(false);

        // Assert
        var mappingGone = await Context.SyncMapping.AnyAsync(m => m.LocalId == listId);
        mappingGone.Should().BeFalse();
    }
}
