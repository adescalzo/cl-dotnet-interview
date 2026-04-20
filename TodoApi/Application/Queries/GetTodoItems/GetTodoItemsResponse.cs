namespace TodoApi.Application.Queries.GetTodoItems;

public sealed record GetTodoItemsResponse(Guid TodoListId, IReadOnlyList<TodoItemResponse> Items);

public sealed record TodoItemResponse(long Id, string Name, bool IsComplete);
