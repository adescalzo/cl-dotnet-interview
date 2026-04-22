using Microsoft.EntityFrameworkCore;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Commands.UpdateTodoItem;

public sealed class UpdateTodoItemHandler(
    ITodoListRepositoryCommand repository,
    ISyncEventRepository syncEvents,
    IClock clock,
    ILogger<UpdateTodoItemHandler> logger
)
{
    public async Task<Result<UpdateTodoItemResponse>> Handle(
        UpdateTodoItemCommand command,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var todoList = await repository
            .GetQueryable(ct: ct)
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == command.TodoListId, ct)
            .ConfigureAwait(false);

        if (todoList is null)
        {
            return Result.Failure<UpdateTodoItemResponse>(
                ErrorResult.NotFound(nameof(TodoList), command.TodoListId.ToString())
            );
        }

        var item = todoList.UpdateItem(command.ItemId, command.Name, clock.UtcNow);
        if (item is null)
        {
            return Result.Failure<UpdateTodoItemResponse>(
                ErrorResult.NotFound(nameof(TodoItem), command.ItemId.ToString())
            );
        }

        if (item.IsComplete)
        {
            return Result.Failure<UpdateTodoItemResponse>(
                ErrorResult.Conflict(nameof(TodoItem), "Item is already completed.")
            );
        }

        var syncEvent = new TodoItemUpdatedPayload(
            item.Id,
            todoList.Id,
            item.Name,
            item.IsComplete
        );
        await syncEvents.AddAsync(SyncEvent.TodoItemUpdated(syncEvent), ct).ConfigureAwait(false);

        logger.LogTodoItemUpdated(todoList.Id, item.Id, item.Name);

        return Result.Success(
            new UpdateTodoItemResponse(item.Id, todoList.Id, item.Name, item.IsComplete)
        );
    }
}

internal static partial class UpdateTodoItemHandlerLoggerDefinition
{
    [LoggerMessage(
        EventId = 600,
        Level = LogLevel.Information,
        EventName = "TodoItemUpdated",
        Message = "TodoItem updated - TodoListId: {TodoListId}, ItemId: {ItemId}, Name: {Name}"
    )]
    public static partial void LogTodoItemUpdated(
        this ILogger logger,
        Guid todoListId,
        Guid itemId,
        string name
    );
}
