namespace TodoApi.Application.Commands.AddTodoItem;

public sealed record AddTodoItemResponse(Guid TodoListId, string Name, bool IsComplete);
