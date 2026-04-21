# NOTES.md — Todo API Synchronization Module

> **Status:** Draft

---

## 1. Architecture Overview

The synchronization module extends the Todo API with two background jobs that run on **Quartz.NET** (ADR-0012). The overall architecture follows the same Clean Architecture layers (ADR-0002) and CQRS pattern (ADR-0008) as the rest of the application.

### Components

```
┌──────────────────────────────────────────────────────┐
│  Command Handlers (Application layer)                │
│  CreateTodoList / AddTodoItem / Update* / Delete*    │
│        │ enqueue SyncEvent (same transaction)        │
│        ▼                                             │
│  sync_events table  ◄──────────────────────────────┐ │
│        │                                            │ │
│        ▼                                            │ │
│  OutboundSyncJob (every 1 min)                      │ │
│    - reads Pending events                           │ │
│    - coalesces by (EntityType, EntityId)            │ │
│    - dispatches via Strategy (ADR-0015)             │ │
│    - calls external API via Refit (ADR-0013)        │ │
│    - retries via Polly (ADR-0014)                   │ │
│    - updates sync_mappings                          │ │
│    - broadcasts progress via SignalR                │ │
│                                                     │ │
│  InboundSyncJob (every 5 min)                       │ │
│    - GET /todolists once                            │ │
│    - creates new external records locally           │ │
│    - detects external deletions                     │ │
│    - detects conflicts (logs, skips)                │ │
└──────────────────────────────────────────────────────┘
         │
         ▼ Refit + Polly
  External Todo API
```

### Key ADRs

| # | Decision |
|---|---|
| ADR-0002 | Clean Architecture layers |
| ADR-0003 | DDD — aggregates, repositories |
| ADR-0005 | EF Core, one DbContext per module |
| ADR-0006 | Result pattern — no exceptions for expected failures |
| ADR-0008 | Wolverine CQRS — handlers, TransactionMiddleware |
| ADR-0012 | Quartz.NET scheduling + SignalR + EF Core scope |
| ADR-0013 | Refit for typed external HTTP client |
| ADR-0014 | Polly for HTTP resilience (retry + circuit breaker) |
| ADR-0015 | Strategy pattern for sync event dispatch |

---

## 2. Design Decisions and Trade-offs

### 2.1 No individual item creation endpoint on the external API

The external API only provides `POST /todolists` — there is no `POST /todolists/{id}/todoitems`. Items can only be created during list creation.

**Decision:** When a new `TodoItem` is added to an already-synced list, the push job:
1. Deletes the existing external list (`DELETE /todolists/{externalId}`) — this cascades items.
2. Recreates the list with all current items including the new one (`POST /todolists`).
3. Updates all `SyncMapping` records to the new external IDs.

**Trade-off:** This is destructive and creates a brief gap where the external list does not exist. It is the only viable option given the current external API contract. A future improvement would require the external API to expose `POST /todolists/{id}/todoitems`.

### 2.2 Event-based push instead of batch version comparison

Rather than scanning all entities for version changes on every sync run, the system records an event at the moment of each mutation. The push job processes these events sequentially, ordered by creation time.

**Advantages:**
- The external system receives changes in the exact order they happened locally — it can always reconstruct the current state.
- No entity-level locking or version comparison needed in the job.
- Failed events remain in the table with their error — full audit trail without additional infrastructure.

**Trade-off:** The event log grows indefinitely if completed events are not pruned. A periodic cleanup job (not in scope) should archive or delete old `Completed` events.

### 2.3 Entity-level isolation — no blocking

Each entity is processed independently in the push job. A failure on one entity (exhausted retries, conflict) marks that event `Failed` and the job moves to the next. Local entities are never locked, flagged, or blocked during sync.

**Consequence:** Multiple failed events for the same entity accumulate in the table. They are retried on the next job run (if marked `Pending` on failure) or remain as `Failed` entries for observability. A future improvement could add auto-retry after a configurable delay.

### 2.4 Conflict detection — fail fast

When the push job detects that an external record changed since the last sync (external `updated_at > mapping.ExternalUpdatedAt`), it classifies this as a conflict. Policy: **log and skip**. Neither side is modified. Manual resolution is required.

The same policy applies in the pull job: if a known external record has a newer `updated_at` than our last sync, it is logged as CONFLICT and skipped.

**Rationale:** Automatic conflict resolution without a deterministic policy (e.g. "last write wins") risks silent data loss. Fail-fast is the safest default.

---

## 3. Resilience Approach

- **Polly retry** (ADR-0014): transient HTTP errors (5xx, 429, timeout) are retried up to 3 times with exponential backoff + jitter. Non-retryable errors (4xx except 429) fail immediately.
- **Circuit breaker**: after 5 consecutive failures, the circuit opens for 30 seconds to protect the external API.
- **Per-entity isolation**: each entity is wrapped in try/catch; one failure does not abort the cycle.
- **GET failure guard**: if `GET /todolists` fails in the pull job, the cycle aborts. Treating a failed fetch as "no external records" would cascade phantom local deletions.

---

## 4. Edge Cases

| Case | Behavior |
|---|---|
| New item added to list that was never synced | Event marked `Failed` (no mapping exists, cannot push item) |
| Delete external list → 404 | Treated as success. Mapping removed. |
| External record absent from GET (inbound deletion) | Local record deleted. Mapping removed. |
| Push job runs while pull job is also running | Both are `[DisallowConcurrentExecution]` independently. They can overlap with each other — this is acceptable since they operate on different concerns (push reads sync_events; pull reads external API). |
| Handler transaction rolls back | No `SyncEvent` is persisted (atomic write). Nothing to push. |
| New local record created during inbound pull | No `SyncEvent` is enqueued. Prevents push job from re-pushing externally-originated records back to the external API. |

---

## 5. Areas for Future Improvement

1. **External API: individual item creation endpoint** — the most impactful improvement. Would eliminate the delete-and-recreate strategy entirely.
2. **External API: version or ETag field** — `updated_at` is a weak change detector. Two updates within the same second are indistinguishable.
3. **External API: `updated_since` filter on GET** — avoids fetching the entire dataset on every pull cycle.
4. **Conflict resolution policy** — currently fail-fast + manual. A "last local write wins" or "last external write wins" policy could be added as configuration.
5. **`Failed` event auto-retry** — retry with a backoff counter rather than leaving events in `Failed` permanently.
6. **Completed event pruning** — archive or delete old `Completed` events to keep the table small.
7. **Pagination on external GET** — the current external API returns all records in one call. Not a problem now; would need pagination support for large datasets.

---

## 6. Out of Scope

- **Versioning on local entities for concurrency control** — a `version` column was added to `TodoList` and `TodoItem` but optimistic locking (HTTP 409 on stale version) was not implemented. Documenting this as a known gap.
- **API versioning** — no `/v1/` prefix or `Accept: application/vnd.*` versioning was added to the local API.
- **Outbox Pattern** — the sync event log resembles an outbox but is not a formal Outbox Pattern implementation. There is no transactional relay, no message broker, and no guaranteed-delivery semantics. Events are written by handlers directly and processed by a polling job. The formal Outbox Pattern (with a dedicated relay and message bus) is out of scope.
- **Authentication on the external API** — the spec states no auth is required.
- **Automatic conflict resolution** — manual resolution required after fail-fast.
- **Multi-tenant / multi-external-system** — single external API instance only.
