namespace TodoApi.Application.Commands.CompleteTodoItem;

public sealed record CompleteTodoItemCommand(Guid TodoListId, Guid ItemId);
