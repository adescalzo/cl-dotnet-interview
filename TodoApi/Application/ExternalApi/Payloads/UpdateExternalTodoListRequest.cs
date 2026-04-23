namespace TodoApi.Application.ExternalApi.Payloads;

public sealed record UpdateExternalTodoListRequest(
    string? Name,
    IReadOnlyList<CreateExternalTodoItemRequest>? Items = null
);
