using Microsoft.EntityFrameworkCore;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Commands.RemoveTodoItem;

public sealed class RemoveTodoItemHandler(
    IRepositoryCommand<TodoList> repository,
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

        var removed = todoList.RemoveItem(command.ItemId, clock.UtcNow);
        if (!removed)
        {
            return Result.Failure(
                ErrorResult.NotFound(nameof(TodoItem), command.ItemId.ToString())
            );
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
        long itemId
    );
}
