namespace TodoApi.Application.Queries.GetTodoLists;

public sealed record GetTodoListsResponse(IReadOnlyList<TodoListSummary> TodoLists);

public sealed record TodoListSummary(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    IReadOnlyList<TodoItemSummary> Items
);

public sealed record TodoItemSummary(long Id, string Name, bool IsComplete);
