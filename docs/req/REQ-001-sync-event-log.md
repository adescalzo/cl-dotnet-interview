# REQ-001: Sync Event Log

**Epic:** EPIC-001  
**Type:** Functional — Critical

---

## Problem Statement

The push sync job needs to know what changed locally and needs to be sent to the external API. Rather than scanning all entities for version changes on every run, the system records a log entry at the moment of each mutation. The job processes these entries and marks them done.

This is **not** the Outbox Pattern (no domain events, no transactional outbox relay). It is a simple append-only event log written by command handlers.

---

## Requirement

Every create, update, and delete on `TodoList` and `TodoItem` must record an entry in a `sync_events` table, within the same database transaction as the entity change.

---

## Specification

### 1. Schema

| Field | Type | Description |
|---|---|---|
| `Id` | Guid (UUIDv7) | Primary key |
| `EntityType` | enum | `TodoList` or `TodoItem` |
| `EntityId` | Guid | Local ID of the affected entity |
| `EventType` | enum | `Created`, `Updated`, `Deleted` |
| `Payload` | string (JSON) | Snapshot of mutable fields at event time |
| `Status` | enum | `Pending`, `Completed`, `Failed` |
| `CreatedAt` | DateTime UTC | When the event was recorded |
| `ProcessedAt` | DateTime UTC (nullable) | When the push job processed it |
| `Error` | string (nullable) | Error detail if `Status = Failed` |

### 2. Payload content per event type

| EntityType + EventType | Payload fields |
|---|---|
| `TodoList / Created` | `{ name, items: [{ description, completed }] }` |
| `TodoList / Updated` | `{ name }` |
| `TodoList / Deleted` | `{}` (empty — external ID resolved from mapping) |
| `TodoItem / Created` | `{ description, completed, todoListId }` |
| `TodoItem / Updated` | `{ description, completed }` |
| `TodoItem / Deleted` | `{}` |

### 3. Transaction guarantee

The event row is written by the command handler before returning. The Wolverine `TransactionMiddleware` calls `SaveChangesAsync` once — both the entity change and the event record commit atomically. If the transaction rolls back, the event is not persisted.

### 4. Delete list — no item events

When a `TodoList` is deleted, **no individual `TodoItem / Deleted` events are recorded**. The external API cascades item deletion when the list is deleted (`DELETE /todolists/{id}`).

### 5. Status transitions

```
Pending → Completed   (push job processed successfully)
Pending → Failed      (push job exhausted retries or detected conflict)
```

Failed events are not retried automatically in the current cycle. They remain in the table for observability. A future improvement could add a retry counter.

### 6. Repository interface

```csharp
Task EnqueueAsync(SyncEvent syncEvent, CancellationToken ct);
Task<IReadOnlyList<SyncEvent>> GetPendingAsync(int batchSize, CancellationToken ct);
Task MarkCompletedAsync(Guid id, CancellationToken ct);
Task MarkFailedAsync(Guid id, string error, CancellationToken ct);
```

---

## Acceptance Criteria

- [ ] After `CreateTodoListHandler` commits, one `Pending` event exists with `EntityType = TodoList`, `EventType = Created`, `EntityId = list.Id`.
- [ ] After `DeleteTodoListHandler` commits, one `Deleted` event exists for the list only — no item events.
- [ ] If the handler transaction rolls back, no event row persists.
- [ ] `GetPendingAsync(10)` returns at most 10 events ordered by `CreatedAt` ascending.
- [ ] `MarkFailedAsync` sets `Status = Failed` and stores the error string.
