namespace TodoApi.Application.ExternalApi.Payloads;

public sealed record CreateExternalTodoListRequest(
    string SourceId,
    string Name,
    IReadOnlyList<CreateExternalTodoItemRequest> Items
);
