using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Refit;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.Jobs.Strategies;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure.Extensions;

namespace TodoApi.Tests.Application.Jobs.Strategies;

public sealed class TodoListDeletedStrategyTests
{
    private readonly IExternalTodoApiClient _client = Substitute.For<IExternalTodoApiClient>();

    private TodoListDeletedStrategy Sut() =>
        new(_client, NullLogger<TodoListDeletedStrategy>.Instance);

    [Fact]
    public async Task ExecuteAsync_WhenCalled_ShouldCallDeleteWithPayloadIdAndCorrelationHeader()
    {
        var id = GuidV7.NewGuid();
        var syncEvent = new SyncEvent(
            EntityType.TodoList,
            id,
            EventType.Deleted,
            JsonSerializer.Serialize(new TodoListDeletedPayload(id))
        );

        await Sut().ExecuteAsync(syncEvent, CancellationToken.None);

        await _client
            .Received(1)
            .DeleteTodoListAsync(
                syncEvent.CorrelationId.ToString(),
                id.ToString(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ExecuteAsync_WhenExternalReturns404_ShouldSwallow()
    {
        var id = GuidV7.NewGuid();
        var syncEvent = new SyncEvent(
            EntityType.TodoList,
            id,
            EventType.Deleted,
            JsonSerializer.Serialize(new TodoListDeletedPayload(id))
        );

        _client
            .DeleteTodoListAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(
                await ApiException.Create(
                    new HttpRequestMessage(),
                    HttpMethod.Delete,
                    new HttpResponseMessage(HttpStatusCode.NotFound),
                    new RefitSettings()
                )
            );

        var act = async () => await Sut().ExecuteAsync(syncEvent, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
