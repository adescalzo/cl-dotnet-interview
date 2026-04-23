# REQ-003: Push Sync — Local to External (Outbound)

**Epic:** EPIC-001  
**Type:** Functional — Critical  
**Depends on:** REQ-001 (Sync Event Log), REQ-002 (Sync Mapping), REQ-007 (Refit Client)

---

## Problem Statement

Local changes must be propagated to the external API. The push job reads pending events from the sync event log and translates them into external API calls.

---

## Requirement

A non-concurrent background job (`OutboundSyncJob`) runs every minute, reads pending `SyncEvent` records, and pushes each change to the external API via the Refit client.

---

## Specification

### 1. Job behavior

- Decorated with `[DisallowConcurrentExecution]` — if already running, the next trigger skips until it finishes.
- Processes events in batches (configurable size, default 50).
- After processing the batch, if events remain, the next scheduled run picks them up.

### 2. Event coalescing

Before processing, group events by `(EntityType, EntityId)`. Within each group, keep only the **last** event by `CreatedAt`. This prevents redundant API calls when an entity is updated multiple times between job runs.

Exception: if the last event is `Deleted`, it takes precedence regardless of earlier events.

### 3. Processing logic per event type

**Created (TodoList):**
- Check mapping: if mapping already exists → skip (idempotent duplicate), mark `Completed`.
- Call `POST /todolists` with `source_id = localId.ToString()`, `name`, and all current items (each with `source_id`).
- On success: store `SyncMapping` for list and each item. Mark event `Completed`.

**Created (TodoItem on already-synced list):**
- The external API has no `POST /todoitems` endpoint. The only creation mechanism is `POST /todolists` (list + items together).
- Strategy: **delete and recreate**.
  1. Load all current items for the parent list from the local repository.
  2. Call `DELETE /todolists/{externalListId}` to remove the old external list (items cascade automatically).
  3. Call `POST /todolists` with `source_id = localListId`, `name`, and **all current items** (including the newly added one), each with their `source_id`.
  4. Update `SyncMapping` for the list to the new `ExternalId`. Update (or create) `SyncMapping` for every item.
  5. Mark event `Completed`.
- If the parent list has no mapping → mark `Failed`, log (cannot push an item for a list that was never synced).
- This strategy is a known trade-off. Document in NOTES.md.

**Updated (TodoList or TodoItem):**
- Look up mapping. If no mapping → mark `Failed`, log (cannot update unknown external record).
- Compare `mapping.ExternalUpdatedAt` with external `updated_at` from cached GET data (loaded once per cycle). If external changed → **conflict**: mark `Failed`, log CONFLICT with local ID, external ID, timestamps.
- No conflict: call `PATCH` with mutable fields only. Update `ExternalUpdatedAt` in mapping. Mark `Completed`.

**Deleted (TodoList):**
- Look up mapping. If no mapping → mark `Completed` (already gone).
- Call `DELETE /todolists/{externalId}`. 404 = success.
- Remove mapping. Mark `Completed`.

**Deleted (TodoItem):**
- Look up mapping. If no mapping → mark `Completed`.
- Call `DELETE /todolists/{externalListId}/todoitems/{externalItemId}`. 404 = success.
- Remove mapping. Mark `Completed`.

### 4. Cycle-level GET cache

To avoid calling `GET /todolists` per-entity for conflict detection, the push job loads external data once at the start of each run (if the pull job has populated a cache, share it; otherwise call once and hold in-memory for the duration of the batch). Do not call `GET /todolists` more than once per push run.

### 5. SignalR broadcast

After each entity is processed, broadcast via `IHubContext<NotificationHub>`:
- Method: `"OutboundSync"`
- Payload: `{ entityType, entityId, eventType, status, error? }`

### 6. Summary log

At end of each run, log INFO:
```
[OutboundSyncJob] Completed: processed=N succeeded=N failed=N conflicts=N
```

---

## Acceptance Criteria

- [ ] New local list → `POST /todolists` called once with `source_id`, list + items. Mapping created.
- [ ] Updated local list → `PATCH /todolists/{id}` called once. `ExternalUpdatedAt` updated in mapping.
- [ ] 3 update events for the same list → only 1 PATCH call (coalescing).
- [ ] Deleted local list → `DELETE /todolists/{id}` called. Mapping removed. No item delete calls.
- [ ] Conflict detected → no PATCH, event marked `Failed`, logged as CONFLICT.
- [ ] Duplicate `Created` event (mapping already exists) → skipped, marked `Completed`.
- [ ] Two concurrent triggers → second skips due to `[DisallowConcurrentExecution]`.
