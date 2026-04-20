namespace TodoApi.Application.Commands.CompleteTodoItem;

public sealed record CompleteTodoItemResponse(
    long Id,
    Guid TodoListId,
    string Name,
    bool IsComplete
);
