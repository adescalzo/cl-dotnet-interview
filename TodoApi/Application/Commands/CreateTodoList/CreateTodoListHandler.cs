using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Commands.CreateTodoList;

/// <summary>
/// Handler for creating a new TodoList aggregate.
/// Wolverine convention: public class with Handle method matching command type.
/// </summary>
public sealed class CreateTodoListHandler(
    ITodoListRepositoryCommand repository,
    IClock clock,
    ILogger<CreateTodoListHandler> logger
)
{
    public async Task<Result<CreateTodoListResponse>> Handle(CreateTodoListCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var todoList = new TodoList(command.Name, clock.UtcNow);
        await repository.AddAsync(todoList, ct).ConfigureAwait(false);

        logger.LogTodoListCreated(todoList.Id, todoList.Name);

        var response = new CreateTodoListResponse(todoList.Id, todoList.Name, todoList.CreatedAt);

        return Result.Success(response);
    }
}

internal static partial class CreateTodoListHandlerLoggerDefinition
{
    [LoggerMessage(
        EventId = 100,
        Level = LogLevel.Information,
        EventName = "TodoListCreated",
        Message = "TodoList created - Id: {Id}, Name: {Name}"
    )]
    public static partial void LogTodoListCreated(this ILogger logger, Guid id, string name);
}
