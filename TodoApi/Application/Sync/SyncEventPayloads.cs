namespace TodoApi.Application.Sync;

public sealed record TodoListCreatedPayload(Guid Id, string Name);

public sealed record TodoListUpdatedPayload(Guid Id, string Name);

public sealed record TodoListDeletedPayload(Guid Id);

public sealed record TodoItemCreatedPayload(Guid Id, Guid TodoListId, string Name, bool IsComplete);

public sealed record TodoItemUpdatedPayload(Guid Id, Guid TodoListId, string Name, bool IsComplete);

public sealed record TodoItemDeletedPayload(Guid Id, Guid TodoListId);
