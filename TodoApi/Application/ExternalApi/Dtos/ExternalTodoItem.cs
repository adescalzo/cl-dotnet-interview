namespace TodoApi.Application.ExternalApi.Dtos;

public sealed record ExternalTodoItem(
    string Id,
    string Description,
    bool Completed,
    string TodoListId,
    DateTime UpdatedAt
);
