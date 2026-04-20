namespace TodoApi.Application.Commands.UpdateTodoList;

public sealed record UpdateTodoListResponse(Guid Id, string Name, DateTime? UpdatedAt);
