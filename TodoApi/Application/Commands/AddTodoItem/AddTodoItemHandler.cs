using Microsoft.EntityFrameworkCore;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Extensions;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Commands.AddTodoItem;

public sealed class AddTodoItemHandler(
    ITodoListRepositoryCommand repository,
    ISyncEventRepository syncEvents,
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

        var id = GuidV7.NewGuid();
        var createdAt = clock.UtcNow;
        var item = todoList.AddItem(id, command.Name, command.Order, createdAt, createdAt);
        var syncEvent = new TodoItemCreatedPayload(
            item.Id,
            todoList.Id,
            item.Name,
            item.IsComplete
        );
        await syncEvents.AddAsync(SyncEvent.TodoItemCreated(syncEvent), ct).ConfigureAwait(false);

        logger.LogTodoItemAdded(todoList.Id, item.Name);

        return Result.Success(
            new AddTodoItemResponse(
                item.Id,
                todoList.Id,
                item.Name,
                item.IsComplete,
                item.Order,
                item.CreatedAt
            )
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
