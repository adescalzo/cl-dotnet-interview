namespace TodoApi.Application.Commands.CreateTodoList;

/// <summary>
/// Command to create a new TodoList.
/// Pure CQRS command - validation is handled at the API layer.
/// </summary>
/// <param name="Name">The name of the TodoList to create.</param>
public sealed record CreateTodoListCommand(string Name);
