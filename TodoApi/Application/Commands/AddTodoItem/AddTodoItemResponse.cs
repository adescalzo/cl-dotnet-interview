namespace TodoApi.Application.Commands.AddTodoItem;

public sealed record AddTodoItemResponse(
    Guid Id,
    Guid TodoListId,
    string Name,
    bool IsComplete,
    int Order,
    DateTime CreatedAt
);
