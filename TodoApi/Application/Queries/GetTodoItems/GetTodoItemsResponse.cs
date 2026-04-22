namespace TodoApi.Application.Queries.GetTodoItems;

public sealed record GetTodoItemsResponse(Guid TodoListId, IReadOnlyList<TodoItemResponse> Items);

public sealed record TodoItemResponse(
    Guid Id,
    string Name,
    bool IsComplete,
    int Order,
    DateTime CreatedAt
);
