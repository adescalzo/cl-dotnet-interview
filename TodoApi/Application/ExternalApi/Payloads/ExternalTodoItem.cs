namespace TodoApi.Application.ExternalApi.Payloads;

public sealed record ExternalTodoItem(
    string Id,
    string? SourceId,
    string Description,
    bool Completed,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
