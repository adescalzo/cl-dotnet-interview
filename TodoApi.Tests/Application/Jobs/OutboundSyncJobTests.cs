using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Dtos;
using TodoApi.Application.Jobs;
using TodoApi.Application.Jobs.Strategies;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Tests.Builders;
using TodoApi.Tests.TestSupport;

namespace TodoApi.Tests.Application.Jobs;

public sealed class OutboundSyncJobCoalesceTests : AsyncLifetimeBase
{
    private IExternalTodoApiClient _client = null!;
    private SyncEventCommandRepository _syncEventRepo = null!;
    private SyncMappingCommandRepository _syncMappingRepo = null!;

    protected override Task OnInitializeAsync()
    {
        _client = Substitute.For<IExternalTodoApiClient>();
        _syncEventRepo = new SyncEventCommandRepository(Context);
        _syncMappingRepo = new SyncMappingCommandRepository(Context);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Handler_WhenTodoListCreated_ShouldEnqueueSyncEvent()
    {
        // Arrange — use handler to enqueue, then verify event is persisted
        var handler = new TodoApi.Application.Commands.CreateTodoList.CreateTodoListHandler(
            new TodoListCommandRepository(Context),
            _syncEventRepo,
            Clock,
            NullLogger<TodoApi.Application.Commands.CreateTodoList.CreateTodoListHandler>.Instance
        );

        var command = new TodoApi.Application.Commands.CreateTodoList.CreateTodoListCommand(
            "Test list"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        await SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();

        var events = await Context.SyncEvent.ToListAsync();
        events.Should().HaveCount(1);
        events[0].EntityType.Should().Be(EntityType.TodoList);
        events[0].EventType.Should().Be(EventType.Created);
        events[0].Status.Should().Be(SyncStatus.Pending);
    }

    [Fact]
    public async Task Strategies_WhenDispatched_ShouldCoalesceMultipleEventsForSameEntity()
    {
        // Arrange — two pending events for the same list entity
        var listId = Guid.NewGuid();

        var older = new SyncEvent(
            EntityType.TodoList,
            listId,
            EventType.Created,
            $"{{\"Id\":\"{listId}\",\"Name\":\"Old\"}}"
        );
        var newer = new SyncEvent(
            EntityType.TodoList,
            listId,
            EventType.Updated,
            $"{{\"Id\":\"{listId}\",\"Name\":\"New\"}}"
        );

        await _syncEventRepo.AddAsync(older);
        await _syncEventRepo.AddAsync(newer);

        var listMapping = new SyncMapping(
            EntityType.TodoList,
            listId,
            "ext-list",
            DateTime.UtcNow.AddHours(-1)
        );
        await _syncMappingRepo.AddAsync(listMapping);

        await SaveChangesAsync();

        var updatedAt = DateTime.UtcNow;
        _client
            .UpdateTodoListAsync(
                Arg.Any<string>(),
                Arg.Any<UpdateExternalTodoListRequest>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ExternalTodoList("ext-list", "New", updatedAt, []));

        var strategies = new ISyncEventStrategy[]
        {
            new TodoListCreatedStrategy(_client, _syncMappingRepo),
            new TodoListUpdatedStrategy(_client, _syncMappingRepo),
        };

        var dispatcher = new SyncEventDispatcher(strategies);

        var pending = await _syncEventRepo.GetPendingAsync(50, CancellationToken.None);

        var coalesced = pending
            .GroupBy(e => e.EntityId)
            .Select(g => g.OrderByDescending(e => e.CreatedAt).First())
            .ToList();

        coalesced.Should().HaveCount(1);
        coalesced[0].EventType.Should().Be(EventType.Updated);

        // Act
        await dispatcher.DispatchAsync(coalesced[0], CancellationToken.None);
        await SaveChangesAsync();

        // Assert: only UpdateTodoListAsync was called, not Create
        await _client
            .Received(1)
            .UpdateTodoListAsync(
                "ext-list",
                Arg.Is<UpdateExternalTodoListRequest>(r => r.Name == "New"),
                Arg.Any<CancellationToken>()
            );

        await _client
            .DidNotReceive()
            .CreateTodoListAsync(
                Arg.Any<CreateExternalTodoListRequest>(),
                Arg.Any<CancellationToken>()
            );
    }
}
