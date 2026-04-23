namespace TodoApi.Application.ExternalApi.Payloads;

public sealed record ExternalTodoList(
    string Id,
    string? SourceId,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<ExternalTodoItem> Items
);
