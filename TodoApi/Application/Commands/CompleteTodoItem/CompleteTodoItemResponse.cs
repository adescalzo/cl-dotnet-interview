namespace TodoApi.Application.Commands.CompleteTodoItem;

public sealed record CompleteTodoItemResponse(
    Guid Id,
    Guid TodoListId,
    string Name,
    bool IsComplete
);
