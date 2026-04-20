namespace TodoApi.Application.Commands.CompleteTodoItem;

public sealed record CompleteTodoItemCommand(Guid TodoListId, long ItemId);
