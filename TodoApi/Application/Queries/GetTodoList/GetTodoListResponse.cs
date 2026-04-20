namespace TodoApi.Application.Queries.GetTodoList;

public sealed record GetTodoListResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    IReadOnlyList<TodoListItemResponse> Items);

public sealed record TodoListItemResponse(long Id, string Name, bool IsComplete);
