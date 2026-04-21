using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Commands.UpdateTodoList;

public sealed class UpdateTodoListHandler(
    ITodoListRepositoryCommand repository,
    IClock clock,
    ILogger<UpdateTodoListHandler> logger
)
{
    public async Task<Result<UpdateTodoListResponse>> Handle(
        UpdateTodoListCommand command,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var todoList = await repository.GetByIdAsync(command.Id, ct: ct).ConfigureAwait(false);
        if (todoList is null)
        {
            return Result.Failure<UpdateTodoListResponse>(
                ErrorResult.NotFound(nameof(TodoList), command.Id.ToString())
            );
        }

        todoList.Update(command.Name, clock.UtcNow);

        logger.LogTodoListUpdated(todoList.Id, todoList.Name);

        return Result.Success(
            new UpdateTodoListResponse(todoList.Id, todoList.Name, todoList.UpdatedAt)
        );
    }
}

internal static partial class UpdateTodoListHandlerLoggerDefinition
{
    [LoggerMessage(
        EventId = 400,
        Level = LogLevel.Information,
        EventName = "TodoListUpdated",
        Message = "TodoList updated - Id: {Id}, Name: {Name}"
    )]
    public static partial void LogTodoListUpdated(this ILogger logger, Guid id, string name);
}
