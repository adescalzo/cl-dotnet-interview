namespace TodoApi.Application.Commands.UpdateTodoItem;

public sealed record UpdateTodoItemResponse(long Id, Guid TodoListId, string Name, bool IsComplete);
