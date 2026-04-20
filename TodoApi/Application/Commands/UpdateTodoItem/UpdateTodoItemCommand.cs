namespace TodoApi.Application.Commands.UpdateTodoItem;

public sealed record UpdateTodoItemCommand(Guid TodoListId, long ItemId, string Name);
