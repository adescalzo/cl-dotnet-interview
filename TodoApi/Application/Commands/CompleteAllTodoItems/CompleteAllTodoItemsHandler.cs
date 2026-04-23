using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TodoApi.Application.Services;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Hubs;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Commands.CompleteAllTodoItems;

public sealed class CompleteAllTodoItemsHandler(
    ITodoListRepositoryCommand repository,
    IBulkOperationTracker tracker,
    IHubContext<NotificationHub> hub,
    IClock clock,
    ILogger<CompleteAllTodoItemsHandler> logger
)
{
    public async Task Handle(CompleteAllTodoItemsCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        tracker.Start(command.TodoListId);

        var completedCount = 0;

        try
        {
            var todoList = await repository
                .GetQueryable(ct: ct)
                .Include(l => l.Items)
                .FirstOrDefaultAsync(l => l.Id == command.TodoListId, ct)
                .ConfigureAwait(false);

            if (todoList is null)
            {
                throw new InvalidOperationException(
                    $"TodoList '{command.TodoListId}' was not found."
                );
            }

            await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);

            completedCount = todoList.CompleteAllItems(clock.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogBulkCompleteFailed(command.TodoListId, ex);

            await hub.Clients.All
                .SendAsync("BulkCompleteFailed", new { listId = command.TodoListId }, ct)
                .ConfigureAwait(false);

            return;
        }
        finally
        {
            tracker.Stop(command.TodoListId);
        }

        await hub.Clients.All
            .SendAsync(
                "BulkCompleteFinished",
                new { listId = command.TodoListId, completedCount },
                ct
            )
            .ConfigureAwait(false);

        logger.LogBulkCompleteFinished(command.TodoListId, completedCount);
    }
}

internal static partial class CompleteAllTodoItemsHandlerLoggerDefinition
{
    [LoggerMessage(
        EventId = 810,
        Level = LogLevel.Information,
        EventName = "BulkCompleteFinished",
        Message = "Bulk complete finished - TodoListId: {TodoListId}, CompletedCount: {CompletedCount}"
    )]
    public static partial void LogBulkCompleteFinished(
        this ILogger logger,
        Guid todoListId,
        int completedCount
    );

    [LoggerMessage(
        EventId = 811,
        Level = LogLevel.Error,
        EventName = "BulkCompleteFailed",
        Message = "Bulk complete failed - TodoListId: {TodoListId}"
    )]
    public static partial void LogBulkCompleteFailed(
        this ILogger logger,
        Guid todoListId,
        Exception ex
    );
}