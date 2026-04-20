namespace TodoApi.Application.Commands.CreateTodoList;

/// <summary>
/// Response for TodoList creation.
/// </summary>
/// <param name="Id">Identifier of the newly created TodoList.</param>
/// <param name="Name">Name of the TodoList.</param>
/// <param name="CreatedAt">UTC timestamp when the TodoList was created.</param>
public sealed record CreateTodoListResponse(Guid Id, string Name, DateTime CreatedAt);
