namespace TodoApi.Application.ExternalApi.Dtos;

public sealed record UpdateExternalTodoItemRequest(string Description, bool Completed);
