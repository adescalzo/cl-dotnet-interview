using Microsoft.EntityFrameworkCore;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Commands.CompleteTodoItem;

public sealed class CompleteTodoItemHandler(
    IRepositoryCommand<TodoList> repository,
    IClock clock,
    ILogger<CompleteTodoItemHandler> logger
)
{
    public async Task<Result<CompleteTodoItemResponse>> Handle(
        CompleteTodoItemCommand command,
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
            return Result.Failure<CompleteTodoItemResponse>(
                ErrorResult.NotFound(nameof(TodoList), command.TodoListId.ToString())
            );
        }

        var item = todoList.CompleteItem(command.ItemId, clock.UtcNow);
        if (item is null)
        {
            return Result.Failure<CompleteTodoItemResponse>(
                ErrorResult.NotFound(nameof(TodoItem), command.ItemId.ToString())
            );
        }

        logger.LogTodoItemCompleted(todoList.Id, item.Id);

        return Result.Success(
            new CompleteTodoItemResponse(item.Id, todoList.Id, item.Name, item.IsComplete)
        );
    }
}

internal static partial class CompleteTodoItemHandlerLoggerDefinition
{
    [LoggerMessage(
        EventId = 700,
        Level = LogLevel.Information,
        EventName = "TodoItemCompleted",
        Message = "TodoItem completed - TodoListId: {TodoListId}, ItemId: {ItemId}"
    )]
    public static partial void LogTodoItemCompleted(
        this ILogger logger,
        Guid todoListId,
        long itemId
    );
}
