namespace TodoApi.Application.ExternalApi.Dtos;

public sealed record CreateExternalTodoItemRequest(string Description, bool Completed);
