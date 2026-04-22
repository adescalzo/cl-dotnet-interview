using Microsoft.EntityFrameworkCore;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Commands.RemoveTodoItem;

public sealed class RemoveTodoItemHandler(
    ITodoListRepositoryCommand repository,
    ISyncEventRepository syncEvents,
    IClock clock,
    ILogger<RemoveTodoItemHandler> logger
)
{
    public async Task<Result> Handle(RemoveTodoItemCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var todoList = await repository
            .GetQueryable(ct: ct)
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == command.TodoListId, ct)
            .ConfigureAwait(false);

        if (todoList is null)
        {
            return Result.Failure(
                ErrorResult.NotFound(nameof(TodoList), command.TodoListId.ToString())
            );
        }

        var item = todoList.Items.FirstOrDefault(i => i.Id == command.ItemId);
        var removed = todoList.RemoveItem(command.ItemId, clock.UtcNow);
        if (!removed)
        {
            return Result.Failure(
                ErrorResult.NotFound(nameof(TodoItem), command.ItemId.ToString())
            );
        }

        if (item is not null)
        {
            await syncEvents
                .AddAsync(
                    SyncEvent.TodoItemDeleted(new TodoItemDeletedPayload(item.Id, todoList.Id)),
                    ct
                )
                .ConfigureAwait(false);
        }

        logger.LogTodoItemRemoved(todoList.Id, command.ItemId);

        return Result.Success();
    }
}

internal static partial class RemoveTodoItemHandlerLoggerDefinition
{
    [LoggerMessage(
        EventId = 800,
        Level = LogLevel.Information,
        EventName = "TodoItemRemoved",
        Message = "TodoItem removed - TodoListId: {TodoListId}, ItemId: {ItemId}"
    )]
    public static partial void LogTodoItemRemoved(
        this ILogger logger,
        Guid todoListId,
        Guid itemId
    );
}
