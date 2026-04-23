# REQ-010: Bulk Complete All Items

**Epic:** EPIC-001  
**Type:** Functional — Real-time feature  
**Depends on:** REQ-008 (Quartz/SignalR infrastructure already wired), existing TodoItem command handlers

---

## Problem Statement

Users need a way to mark all items in a list as completed in one action. Because this operation takes several seconds (simulated delay to showcase real-time updates), individual item mutations must be blocked while it runs to prevent race conditions. The frontend must reflect the locked state and update automatically when the operation finishes.

---

## Requirement

A "Complete All" operation runs per list, locks individual item mutations for that list while active, notifies clients via SignalR on completion, and releases the lock unconditionally via `finally`.

---

## Specification

### 1. Bulk operation tracker service

Interface: `IBulkOperationTracker` — registered as **singleton**.

```csharp
interface IBulkOperationTracker
{
    void Start(Guid listId);
    void Stop(Guid listId);
    bool IsRunning(Guid listId);
}
```

Implementation: `ConcurrentDictionary<Guid, byte>` — `Start` adds the key, `Stop` removes it, `IsRunning` checks `ContainsKey`. In-memory, process-scoped. Acceptable for single-instance deployment (per NOTES.md §6 Assumptions).

### 2. Lock status endpoint

```
GET /api/todolists/{listId}/items/status
```

Response `200 OK`:
```json
{ "isLocked": true }
```

`isLocked` is `true` when `IBulkOperationTracker.IsRunning(listId)` returns `true`.

This endpoint is read-only and requires no authentication. It is called by the frontend on mount and after SignalR events.

### 3. Guard in existing item command handlers

The following handlers must inject `IBulkOperationTracker` and return `400 Bad Request` (via `Result.Failure` with `ErrorResult.Conflict` or `ErrorResult.Validation`) if `IsRunning(listId)` is `true` at the start of `Handle`:

- `AddTodoItemHandler`
- `UpdateTodoItemHandler`
- `CompleteTodoItemHandler`
- `RemoveTodoItemHandler`

Error code: `"BulkOperationInProgress"`. Message: `"A bulk operation is currently running for this list."`.

> **NOTES.md addition (§2.x):** The guard above is injected directly in each handler, which is the minimal implementation. The complete production approach would extract this into a Wolverine middleware (pipeline behavior) activated via a `[GuardAgainstBulkOperation]` attribute on the command record — this avoids touching each handler and makes the policy declarative. Out of scope for this version.

### 4. Complete-all endpoint and handler

```
PUT /api/todolists/{listId}/items/complete-all
```

Response: `204 No Content` on success.

**Handler behavior (`CompleteAllTodoItemsHandler`):**

1. Call `tracker.Start(listId)`.
2. Wrap the body in `try/finally` — `tracker.Stop(listId)` always runs in `finally`.
3. Load the `TodoList` with items. If not found → return `NotFound` (after releasing lock in finally).
4. `await Task.Delay(TimeSpan.FromSeconds(10), ct)` — simulates work, allows the frontend to observe the locked state.
5. Call `todoList.CompleteAllItems(clock.UtcNow)` — marks every incomplete item as complete.
6. Persist via the existing transaction middleware path (no explicit `SaveChangesAsync`).
7. Broadcast via `IHubContext<NotificationHub>`:
   - Method: `"BulkCompleteFinished"`
   - Payload: `{ listId, completedCount }` where `completedCount` is the number of items that were incomplete before step 5.
8. Return `Result.Success()`.

**`TodoList.CompleteAllItems(DateTime now)`** — domain method on the aggregate. Iterates `Items`, calls `item.Complete(now)` on each incomplete item, returns the count of items changed. Does not raise domain events.

### 5. SignalR contract

| Method | Direction | Payload |
|---|---|---|
| `BulkCompleteFinished` | Server → Client | `{ listId: string, completedCount: number }` |

No `BulkCompleteStarted` event — the frontend initiates the request so it knows it started.

### 6. Registration

```csharp
services.AddSingleton<IBulkOperationTracker, BulkOperationTracker>();
```

---

## Acceptance Criteria

- [ ] `PUT .../complete-all` on a list with 3 incomplete items: after ~10 s, all 3 are `isComplete = true`. `204` returned. SignalR fires `BulkCompleteFinished` with `completedCount = 3`.
- [ ] During the 10 s window: `PUT .../items/{id}/complete` on any item of that list returns `400` with code `BulkOperationInProgress`.
- [ ] `GET .../items/status` returns `{ isLocked: true }` during the window, `{ isLocked: false }` before and after.
- [ ] If the handler throws mid-operation, `IBulkOperationTracker.Stop` is still called (finally).
- [ ] Two concurrent `PUT .../complete-all` calls for the same list: second call starts its own delay and tracker entry. Acceptable — the second call is redundant but not destructive. (Out of scope to guard against this in v1.)
- [ ] Two concurrent calls for **different** lists: both run independently. No cross-list interference.
