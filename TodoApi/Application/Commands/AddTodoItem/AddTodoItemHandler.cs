using Microsoft.EntityFrameworkCore;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Commands.AddTodoItem;

public sealed class AddTodoItemHandler(
    IRepositoryCommand<TodoList> repository,
    IClock clock,
    ILogger<AddTodoItemHandler> logger
)
{
    public async Task<Result<AddTodoItemResponse>> Handle(
        AddTodoItemCommand command,
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
            return Result.Failure<AddTodoItemResponse>(
                ErrorResult.NotFound(nameof(TodoList), command.TodoListId.ToString())
            );
        }

        var item = todoList.AddItem(command.Name, clock.UtcNow);

        logger.LogTodoItemAdded(todoList.Id, item.Name);

        return Result.Success(
            new AddTodoItemResponse(todoList.Id, item.Name, item.IsComplete)
        );
    }
}

internal static partial class AddTodoItemHandlerLoggerDefinition
{
    [LoggerMessage(
        EventId = 500,
        Level = LogLevel.Information,
        EventName = "TodoItemAdded",
        Message = "TodoItem added - TodoListId: {TodoListId}, Name: {Name}"
    )]
    public static partial void LogTodoItemAdded(this ILogger logger, Guid todoListId, string name);
}
