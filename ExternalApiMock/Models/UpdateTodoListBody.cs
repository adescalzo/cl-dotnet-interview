namespace ExternalApiMock.Models;

public sealed record UpdateTodoListBody(
    string? Name,
    IReadOnlyList<CreateTodoItemBody>? Items = null
);
