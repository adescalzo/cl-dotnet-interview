namespace TodoApi.Application.Commands.RemoveTodoItem;

public sealed record RemoveTodoItemCommand(Guid TodoListId, long ItemId);
