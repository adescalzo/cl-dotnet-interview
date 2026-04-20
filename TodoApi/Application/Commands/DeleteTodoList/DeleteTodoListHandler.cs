using Microsoft.Extensions.Logging;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Commands.DeleteTodoList;

public sealed class DeleteTodoListHandler(
    IRepositoryCommand<TodoList> repository,
    ILogger<DeleteTodoListHandler> logger
)
{
    public async Task<Result> Handle(DeleteTodoListCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var todoList = await repository.GetByIdAsync(command.Id, ct: ct).ConfigureAwait(false);
        if (todoList is null)
        {
            return Result.Failure(ErrorResult.NotFound(nameof(TodoList), command.Id.ToString()));
        }

        repository.Remove(todoList);

        logger.LogTodoListDeleted(command.Id);

        return Result.Success();
    }
}

internal static partial class DeleteTodoListHandlerLoggerDefinition
{
    [LoggerMessage(
        EventId = 300,
        Level = LogLevel.Information,
        EventName = "TodoListDeleted",
        Message = "TodoList deleted - Id: {Id}"
    )]
    public static partial void LogTodoListDeleted(this ILogger logger, Guid id);
}
