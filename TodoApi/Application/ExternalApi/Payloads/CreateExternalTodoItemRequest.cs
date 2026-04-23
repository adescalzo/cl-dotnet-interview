namespace TodoApi.Application.ExternalApi.Payloads;

public sealed record CreateExternalTodoItemRequest(
    string SourceId,
    string Description,
    bool Completed
);
