namespace TodoApi.Application.ExternalApi.Dtos;

public sealed record ExternalTodoList(
    string Id,
    string Name,
    DateTime UpdatedAt,
    IReadOnlyList<ExternalTodoItem> TodoItems
);
