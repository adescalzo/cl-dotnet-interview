namespace TodoApi.Application.Commands.UpdateTodoItem;

public sealed record UpdateTodoItemResponse(Guid Id, Guid TodoListId, string Name, bool IsComplete);
