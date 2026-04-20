namespace TodoApi.Application.Commands.AddTodoItem;

public sealed record AddTodoItemCommand(Guid TodoListId, string Name);
