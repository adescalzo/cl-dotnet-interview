using Microsoft.EntityFrameworkCore;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Commands.DeleteTodoList;

public sealed class DeleteTodoListHandler(
    ITodoListRepositoryCommand repository,
    ISyncEventRepository syncEvents,
    IClock clock,
    ILogger<DeleteTodoListHandler> logger
)
{
    public async Task<Result> Handle(DeleteTodoListCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var todoList = await repository
            .GetQueryable(ct: ct)
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == command.Id, ct)
            .ConfigureAwait(false);

        if (todoList is null)
        {
            return Result.Failure(ErrorResult.NotFound(nameof(TodoList), command.Id.ToString()));
        }

        todoList.MarkAsDeleted(clock.UtcNow);

        var syncEvent = new TodoListDeletedPayload(todoList.Id);
        await syncEvents.AddAsync(SyncEvent.TodoListDeleted(syncEvent), ct).ConfigureAwait(false);

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
