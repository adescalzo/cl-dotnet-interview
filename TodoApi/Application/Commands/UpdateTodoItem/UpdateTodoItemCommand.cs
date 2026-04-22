namespace TodoApi.Application.Commands.UpdateTodoItem;

public sealed record UpdateTodoItemCommand(Guid TodoListId, Guid ItemId, string Name);
