using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Commands.CreateTodoList;

public sealed class CreateTodoListHandler(
    ITodoListRepositoryCommand repository,
    ISyncEventRepository syncEvents,
    IClock clock,
    ILogger<CreateTodoListHandler> logger
)
{
    public async Task<Result<CreateTodoListResponse>> Handle(
        CreateTodoListCommand command,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var todoList = new TodoList(command.Name, clock.UtcNow);
        await repository.AddAsync(todoList, ct).ConfigureAwait(false);

        var syncEvent = new TodoListCreatedPayload(todoList.Id, todoList.Name);
        await syncEvents.AddAsync(SyncEvent.TodoListCreated(syncEvent), ct).ConfigureAwait(false);

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
