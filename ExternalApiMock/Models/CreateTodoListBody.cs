namespace ExternalApiMock.Models;

public sealed record CreateTodoListBody(string? SourceId, string? Name, List<CreateTodoItemBody>? Items);
