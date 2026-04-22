namespace ExternalApiMock.Models;

public sealed record CreateTodoItemBody(string? SourceId, string? Description, bool? Completed);
