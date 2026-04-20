namespace TodoApi.Application.Commands.UpdateTodoList;

public sealed record UpdateTodoListCommand(Guid Id, string Name);
