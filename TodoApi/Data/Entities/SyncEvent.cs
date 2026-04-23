using System.Text.Json;
using TodoApi.Application.Sync;
using TodoApi.Infrastructure.Extensions;

namespace TodoApi.Data.Entities;

public class SyncEvent
{
    private SyncEvent()
    {
        Payload = string.Empty;
    }

    public SyncEvent(EntityType entityType, Guid entityId, EventType eventType, string payload)
    {
        EntityType = entityType;
        EntityId = entityId;
        EventType = eventType;
        Payload = payload;
        Status = SyncStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; } = GuidV7.NewGuid();

    public Guid CorrelationId { get; private set; } = GuidV7.NewGuid();

    public EntityType EntityType { get; private set; }

    public Guid EntityId { get; private set; }

    public EventType EventType { get; private set; }

    public string Payload { get; private set; }

    public SyncStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? ProcessedAt { get; private set; }

    public string? Error { get; private set; }

    public void MarkCompleted(DateTime processedAt)
    {
        Status = SyncStatus.Completed;
        ProcessedAt = processedAt;
    }

    public void MarkFailed(string error, DateTime processedAt)
    {
        Status = SyncStatus.Failed;
        Error = error;
        ProcessedAt = processedAt;
    }

    public static SyncEvent TodoListCreated(TodoListCreatedPayload data)
    {
        return new SyncEvent(
            EntityType.TodoList,
            data.Id,
            EventType.Created,
            JsonSerializer.Serialize(data)
        );
    }

    public static SyncEvent TodoListUpdated(TodoListUpdatedPayload data)
    {
        return new SyncEvent(
            EntityType.TodoList,
            data.Id,
            EventType.Updated,
            JsonSerializer.Serialize(data)
        );
    }

    public static SyncEvent TodoListDeleted(TodoListDeletedPayload data)
    {
        return new SyncEvent(
            EntityType.TodoList,
            data.Id,
            EventType.Deleted,
            JsonSerializer.Serialize(data)
        );
    }

    public static SyncEvent TodoItemCreated(TodoItemCreatedPayload data)
    {
        return new SyncEvent(
            EntityType.TodoItem,
            data.Id,
            EventType.Created,
            JsonSerializer.Serialize(data)
        );
    }

    public static SyncEvent TodoItemUpdated(TodoItemUpdatedPayload data)
    {
        return new SyncEvent(
            EntityType.TodoItem,
            data.Id,
            EventType.Updated,
            JsonSerializer.Serialize(data)
        );
    }

    public static SyncEvent TodoItemDeleted(TodoItemDeletedPayload data)
    {
        return new SyncEvent(
            EntityType.TodoItem,
            data.Id,
            EventType.Deleted,
            JsonSerializer.Serialize(data)
        );
    }
}
