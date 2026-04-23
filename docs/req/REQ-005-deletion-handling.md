# REQ-005: Deletion Handling (Both Directions)

**Epic:** EPIC-001  
**Type:** Functional — Critical  
**Depends on:** REQ-001 (Sync Event Log), REQ-002 (Sync Mapping), REQ-003 (Push Job), REQ-004 (Pull Job)

---

## Problem Statement

Records deleted on the local side must be removed from the external API. Records deleted on the external side must be removed locally. Both scenarios must be idempotent and must not leave orphaned mappings.

---

## Requirement

Deletions must propagate bidirectionally. The system must handle cases where the external record is already gone (404) without failing.

---

## Specification

### 1. Outbound deletion (local → external)

Triggered by `TodoList / Deleted` or `TodoItem / Deleted` events in the sync event log.

**TodoList deletion:**
1. Look up `SyncMapping` for `(TodoList, localId)`. If not found → no external record to delete → mark event `Completed`.
2. Call `DELETE /todolists/{externalId}`.
3. HTTP 204 or 404 → success. Remove mapping. Mark event `Completed`.
4. HTTP 5xx → retry via Polly (REQ-006). After max retries → mark event `Failed`.
5. **Do not call `DELETE` for individual items.** The external API cascades item deletion when the list is deleted.

**TodoItem deletion:**
1. Look up `SyncMapping` for `(TodoItem, localId)`. If not found → mark event `Completed`.
2. Look up parent list mapping to get `externalListId`.
3. Call `DELETE /todolists/{externalListId}/todoitems/{externalItemId}`.
4. HTTP 204 or 404 → success. Remove item mapping. Mark event `Completed`.

### 2. Inbound deletion (external → local)

Detected by `InboundSyncJob` (REQ-004) when a mapped external record is absent from `GET /todolists`.

1. For each `SyncMapping` of type `TodoList` whose `ExternalId` is not in the GET response:
   - Delete the local `TodoList` (cascade deletes items via EF Core).
   - Remove the `TodoList` mapping and all `TodoItem` mappings for that list.
2. Do not treat a GET failure as "all records deleted" — if GET fails, abort. No deletions.

### 3. Idempotency

- 404 on any DELETE call → treat as success. The record is already gone.
- Double-delete (event processed twice due to failure retry): mapping lookup returns null → skip gracefully.

---

## Acceptance Criteria

- [ ] Local list deleted → `DELETE /todolists/{id}` called on external. Mapping removed. No item DELETE calls.
- [ ] Local item deleted → `DELETE /todolists/{listId}/todoitems/{itemId}` called. Mapping removed.
- [ ] 404 on DELETE → treated as success. Mapping removed. Event marked `Completed`.
- [ ] External list absent from GET → local list (and items) deleted. List and item mappings removed.
- [ ] GET failure → no local deletions. Cycle aborts.
- [ ] No mapping found for a delete event → event marked `Completed` without API call.
