using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Payloads;
using TodoApi.Application.Jobs.Strategies;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure.Extensions;

namespace TodoApi.Tests.Application.Jobs.Strategies;

public sealed class TodoListCreatedStrategyTests
{
    private readonly IExternalTodoApiClient _client = Substitute.For<IExternalTodoApiClient>();

    private TodoListCreatedStrategy Sut() =>
        new(_client, NullLogger<TodoListCreatedStrategy>.Instance);

    [Fact]
    public void CanHandle_WhenTodoListCreated_ShouldReturnTrue()
    {
        var evt = new SyncEvent(EntityType.TodoList, GuidV7.NewGuid(), EventType.Created, "{}");
        Sut().CanHandle(evt).Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WhenTodoListUpdated_ShouldReturnFalse()
    {
        var evt = new SyncEvent(EntityType.TodoList, GuidV7.NewGuid(), EventType.Updated, "{}");
        Sut().CanHandle(evt).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCalled_ShouldPostPayloadToExternalApiWithCorrelationHeader()
    {
        var id = GuidV7.NewGuid();
        var payload = new TodoListCreatedPayload(id, "Groceries");
        var syncEvent = new SyncEvent(
            EntityType.TodoList,
            id,
            EventType.Created,
            JsonSerializer.Serialize(payload)
        );

        _client
            .CreateTodoListAsync(
                Arg.Any<string>(),
                Arg.Any<CreateExternalTodoListRequest>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new ExternalTodoList(
                    "ext-1",
                    id.ToString(),
                    "Groceries",
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    []
                )
            );

        await Sut().ExecuteAsync(syncEvent, CancellationToken.None);

        await _client
            .Received(1)
            .CreateTodoListAsync(
                syncEvent.CorrelationId.ToString(),
                Arg.Is<CreateExternalTodoListRequest>(r =>
                    r.SourceId == id.ToString() && r.Name == "Groceries"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ExecuteAsync_WhenExternalApiThrows_ShouldPropagate()
    {
        var id = GuidV7.NewGuid();
        var payload = new TodoListCreatedPayload(id, "x");
        var syncEvent = new SyncEvent(
            EntityType.TodoList,
            id,
            EventType.Created,
            JsonSerializer.Serialize(payload)
        );

        _client
            .CreateTodoListAsync(
                Arg.Any<string>(),
                Arg.Any<CreateExternalTodoListRequest>(),
                Arg.Any<CancellationToken>()
            )
            .Returns<ExternalTodoList>(_ => throw new HttpRequestException("boom"));

        var act = async () => await Sut().ExecuteAsync(syncEvent, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
