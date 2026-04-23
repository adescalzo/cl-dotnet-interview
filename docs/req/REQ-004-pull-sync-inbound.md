# REQ-004: Pull Sync — External to Local (Inbound)

**Epic:** EPIC-001  
**Type:** Functional — Critical  
**Depends on:** REQ-002 (Sync Mapping), REQ-007 (Refit Client)

---

## Problem Statement

The external API may contain records created directly on that system (no `source_id`), or records that were modified externally after the last sync. The pull job detects these cases and acts on them.

---

## Requirement

A non-concurrent background job (`InboundSyncJob`) runs every 5 minutes, fetches all external records once, and applies the appropriate local action for each case.

---

## Specification

### 1. Job behavior

- Decorated with `[DisallowConcurrentExecution]`.
- Calls `GET /todolists` exactly once per run. If this call fails → abort cycle, log ERROR, make no local writes (phantom deletion guard).
- Processes the full response in memory.

### 2. Classification of external records

For each external `TodoList` in the response:

| Case | Condition | Action |
|---|---|---|
| **New external** | `ExternalId` not in `sync_mappings` | Create local record + items. Store mappings. |
| **Known, changed externally** | `ExternalId` in mappings AND external `updated_at > mapping.ExternalUpdatedAt` | Log CONFLICT, skip. Do not modify either side. (Update strategy is a future ADR.) |
| **Known, unchanged** | `ExternalId` in mappings AND `updated_at == mapping.ExternalUpdatedAt` | Skip. No action. |

For each item within a new external list: apply the same classification logic at item level.

### 3. Inbound deletion detection

After processing all external records, compare:
- All `SyncMapping` records of type `TodoList`
- External list IDs returned by GET

Any mapping whose `ExternalId` is **not present** in the GET response → the external record was deleted.
Action: delete the local record (and its items via cascade). Remove the mapping.

Same logic for `TodoItem` mappings within their parent list.

### 4. New external record creation

When creating a local record from an external source:
- Use the external record's fields directly (`name`, `description`, `completed`).
- Assign a new local `Id` (GuidV7).
- Do **not** enqueue a `SyncEvent` for the creation — this would cause the push job to push the record back to the external API, creating a loop.
- Store `SyncMapping` with `ExternalId` and `ExternalUpdatedAt = external.updated_at`.

### 5. SignalR broadcast

After processing, broadcast via `IHubContext<NotificationHub>`:
- Method: `"InboundSync"`
- Payload: `{ created, conflicts, deleted }`

### 6. Summary log

```
[InboundSyncJob] Completed: created=N conflicts=N deleted=N
```

---

## Acceptance Criteria

- [ ] Empty local DB + populated external API → all external lists and items created locally. Mappings stored.
- [ ] Known external list with `updated_at` unchanged → no local modification.
- [ ] Known external list with `updated_at` advanced → logged as CONFLICT. Local record unchanged.
- [ ] External list absent from GET but in `sync_mappings` → local record deleted. Mapping removed.
- [ ] `GET /todolists` called exactly once per run.
- [ ] GET failure → no local writes. Error logged. Cycle aborted.
- [ ] New local records created during inbound pull do NOT produce `SyncEvent` entries.

---

## Constraints

- External records with `source_id` matching a local ID are still treated as "external records" for pull purposes — the mapping table is the authority, not `source_id`.
- Do not assume `source_id` is always populated. Handle null gracefully.
