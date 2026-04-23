namespace TodoApi.Application.ExternalApi.Payloads;

public sealed record UpdateExternalTodoItemRequest(string? Description, bool? Completed);
