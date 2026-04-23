# NOTES.md — Todo API Synchronization Module

> **Status:** Draft

---

## Sobre este proyecto y cómo fue construido

Este challenge fue encarado como una oportunidad real de aprendizaje, no solo como un ejercicio para cumplir. El objetivo fue construir algo que tuviera sentido arquitectónico, que fuera mantenible, y que reflejara cómo trabajaría en un proyecto de producción.

### Uso de IA (Claude)

Desde el principio decidí aprovechar Claude como herramienta de desarrollo. El stack de .NET con Clean Architecture, DDD y CQRS es el área donde tengo más experiencia, así que la IA fue utilizada principalmente como un par de programación: para discutir decisiones de diseño, generar código siguiendo los patrones establecidos, y validar que las implementaciones fueran consistentes con la arquitectura.

El flujo de trabajo fue deliberado: primero definir la arquitectura (ADRs), luego escribir requerimientos (REQs), y recién después pasar al código. Cada sesión con la IA arrancaba con contexto claro sobre qué se quería hacer y por qué. No fue "generame una app", fue "dado este diseño, implementá esto siguiendo estas restricciones".

### El rol de los ADRs

Los Architecture Decision Records fueron una parte central del proceso, no documentación post-hoc. Cada decisión relevante — desde la elección de Wolverine como dispatcher hasta la estrategia para manejar los IDs externos — fue discutida y documentada antes de escribir la primera línea de código. Esto permitió que la IA trabajara dentro de un marco bien definido y que las decisiones fueran trazables.

Los ADRs están en `docs/adr/` (ADR-0001 a ADR-0016) y cubren desde la arquitectura modular hasta decisiones de resiliencia y testing.

### El frontend

El frontend es donde la situación fue diferente. React no es mi área fuerte — mi dominio principal es el backend .NET. En ese contexto, el desarrollo del frontend fue prácticamente íntegro con AI. No fue copy-paste sin entender: cada componente fue revisado, las decisiones de estado y las abstracciones fueron discutidas, y el resultado sigue las buenas prácticas de React 19 (hooks modernos como `use`, `useOptimistic`, `useActionState`, separación de responsabilidades, etc.).

Lo que me importó fue que el resultado fuera correcto y mantenible, aunque el camino para llegar ahí haya sido más asistido que en el backend.

### En resumen

Aprovechar IA no fue un atajo para evitar pensar — fue una forma de producir más y mejor dentro del tiempo disponible. Las decisiones de arquitectura son propias, la documentación es deliberada, y el código sigue estándares que elegiría en un proyecto real.

---

## 0. Running the App

Cada repositorio tiene su propio devcontainer. La forma recomendada es abrir cada uno en VS Code con Dev Containers y correr los procesos desde terminales dentro del container.

### Ports expuestos

| Servicio | Puerto |
|---|---|
| API (.NET) | `5083` |
| ExternalApiMock | `3000` |
| React frontend | `5173` |

---

### cl-dotnet-interview (API + ExternalApiMock)

**Abrir el devcontainer:**

1. Abrir VS Code en la carpeta `cl-dotnet-interview`
2. `Ctrl+Shift+P` → `Dev Containers: Reopen in Container`
3. Esperar que el container levante (instala .NET 8, restaura paquetes, compila)

El devcontainer incluye SQL Server como servicio separado (`sqlserver`). Las migraciones se aplican automáticamente al iniciar la API.


**Terminal 1 — ExternalApiMock:**

```bash
dotnet run --project ExternalApiMock
```

Corre en `http://localhost:3000`. Seed con 2 listas y 3 ítems. Datos in-memory: se resetean al reiniciar.

**Terminal 2 — API:**

```bash
dotnet run --project TodoApi --launch-profile http
```

Corre en `http://localhost:5083`. Al arrancar aplica migraciones pendientes.

---

### cl-react-interview (Frontend)

**Abrir el devcontainer:**

1. Abrir VS Code en la carpeta `cl-react-interview`
2. `Ctrl+Shift+P` → `Dev Containers: Reopen in Container`
3. El `postCreateCommand` corre `npm install` automáticamente

**Terminal 1 — Dev server:**

```bash
npm run dev
```

Corre en `http://localhost:5173`. Requiere que la API esté corriendo en `http://localhost:5083`.

---

### Resetear el DB

**Opción A — solo borrar ítems inyectados por el mock** (mantiene datos propios):

Desde una terminal en el devcontainer de `cl-dotnet-interview`, conectarse con `sqlcmd`:

```bash
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P Password123 -No \
  -Q "DELETE FROM TodoItems WHERE Name LIKE '[INJECT]%'"
```

**Opción B — drop y recrear el DB completo:**

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet ef database drop --project TodoApi --force
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --project TodoApi
```

O simplemente reiniciar la API: aplica migraciones pendientes automáticamente.

---

### Notas de configuración

| Setting | Valor |
|---|---|
| API URL | `http://localhost:5083` |
| ExternalApiMock URL | `http://localhost:3000` |
| React frontend | `http://localhost:5173` |
| InboundSyncJob (dev) | cada 1 hora (`0 0 * * * ?` en `appsettings.Development.json`) |
| OutboundSyncJob | cron en `appsettings.json` sección `Jobs` |

El InboundSyncJob está limitado a 1 vez/hora en dev porque el mock inyecta ítems aleatorios con ~70% de probabilidad en cada llamada a `GET /todolists`, lo que acumula registros `[INJECT]` rápidamente si el job corre frecuente.

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

### 2.1 Item creation via `PATCH /todolists/{id}` — the update endpoint also adds items

The external API contract has **no per-item creation endpoint**. The spec defines create only for lists (`POST /todolists`, optionally with `items[]` in the body). For items that are added to an **already-synced** list, we cannot POST — that would either error or create a duplicate list — and we cannot PATCH an item that does not yet exist externally.

**Decision:** When a `TodoItem` is added locally, `TodoItemCreatedStrategy`:
1. Reads **only the sync-event payload** — `(localItemId, localListId, name, isComplete)`. It does **not** load the parent list or any sibling items from the DB.
2. Calls `PATCH /todolists/{localListId}` with a body carrying `items: [ { source_id = localItemId, description, completed } ]` and `name: null`. The URL uses our local list id; the external API resolves it via `source_id`.
3. Reads the returned list, finds the item whose `source_id` matches the one we sent, and saves (or updates) the item's `SyncMapping` using the external item `id` and `updated_at`.

**Assumption — `PATCH /todolists/{id}` updates the list *and* adds new items:** the endpoint is treated as a partial update that (a) updates scalar fields of the list (`name`) when present, and (b) appends any items in `items[]` whose `source_id` is not already present on the external side. Items whose `source_id` already exists are ignored (no duplicates, no replace). This behavior is outside the strict reading of the OpenAPI spec, which types `UpdateTodoListBody` as `{ name }` only — we extend it with `items[]` and assume the real external API mirrors the mock (`ExternalApiMock/Endpoints/TodoListsEndpoints.cs`). If the real API rejects unknown fields, this assumption breaks and the design needs revisiting.

**Design rule — strategies never read the DB.** Every sync strategy takes its inputs from the `SyncEvent` payload alone. If a strategy needs data that is not in the payload (for example, the parent list's name on an item event), the **command handler** that enqueues the event is responsible for capturing it at write-time and including it in the payload (`TodoItemCreatedPayload`, `TodoItemUpdatedPayload`, etc.). Consequences:
- The sync module is decoupled from the aggregate's read shape — future schema changes don't ripple into strategies.
- Every push is self-contained: the event captures exactly what was true at write-time, not what the DB happens to look like later.
- No accidental "resubmit the whole list" side effects, and no conflation of "I added one item" with "here is the full list".

**Item delete / item update** continue to use the per-item endpoints that *do* exist in the spec: `DELETE /todolists/{listId}/todoitems/{itemId}` and `PATCH /todolists/{listId}/todoitems/{itemId}`. Those require an item mapping, which item-create populates.

**Edge case — list not yet synced:** if the `TodoItemCreated` event reaches the push job before the list's create event has been processed (or has failed), the PATCH returns 404. The event is marked `Failed` and retried on the next cycle once the list exists externally. FIFO event processing means this is rare but not impossible.

**Trade-off:** We depend on a non-standard behavior of the PATCH endpoint. In exchange we get: no DB reads in the sync path, no "resubmit the whole list" bandwidth, and a clean separation where each event type maps to exactly one external call.

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

### 2.5 Bulk operation lock — handler-level guard vs. Wolverine middleware

The `IBulkOperationTracker` guard is injected directly into each item command handler. This is the minimal implementation. The complete production approach would extract this into a Wolverine pipeline middleware activated via a `[GuardAgainstBulkOperation]` attribute on the command record — keeping handlers clean and making the policy declarative. Out of scope for this version.

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
5. **`Failed` event attempt counter** — currently a `Failed` event stays in the table and is retried on every subsequent job run without a limit. Adding an `AttemptCount` field with a configurable max would allow marking events as permanently failed after N attempts, preventing indefinite retry loops. Not implemented in this version — the current behavior (keep retrying) is acceptable for the scope of this challenge.
6. **Completed event pruning** — archive or delete old `Completed` events to keep the table small.
7. **Pagination on external GET** — the current external API returns all records in one call. Not a problem now; would need pagination support for large datasets.

---

## 6. Assumptions

### External API contract

- **No pagination on GET /todolists** — the spec returns all lists and items in a single response. The pull job calls it exactly once per cycle and holds the result in memory for the duration of that run. If the external API adds pagination in the future, the pull job will need to be updated.
- **`updated_at` as the only change signal** — the external API has no `version` or `ETag` field. We use `updated_at` to detect external changes. Two updates to the same record within the same second are indistinguishable — we accept this as a known limitation.
- **`source_id` is write-once** — the external API's PATCH endpoints (`UpdateTodoListBody`, `UpdateTodoItemBody`) do not accept `source_id`. We never attempt to set it after creation.
- **DELETE cascades items** — deleting a `TodoList` on the external API removes all its `TodoItem`s. We rely on this behavior and do not send individual item delete events when a list is deleted.
- **404 on DELETE = success** — if the external record is already gone, we treat 404 as a successful idempotent delete and remove the local mapping.
- **No individual item creation endpoint** — the external API only allows creating items as part of a `POST /todolists`. For items added to already-synced lists, we use a delete-and-recreate strategy (delete the external list, POST it again with all current items). This is the only viable approach given the current contract.

### Sync behavior

- **`sync_mappings` is the authority** — we determine whether a record is "ours" by looking it up in `sync_mappings`, not by inspecting `source_id`. External records with `source_id` set but no entry in our mappings are treated as new external records to import.
- **Events processed in FIFO order** — sync events are consumed in `CreatedAt` ascending order. This preserves the sequence of local changes so the external system receives them in the order they happened.
- **No inbound sync event** — records created locally during the pull job (inbound sync) do not produce `SyncEvent` entries. This prevents the push job from pushing externally-originated records back to the external API.
- **Failed events retry indefinitely** — a `Failed` event remains in the table with `Status = Failed` and is retried on every subsequent push job run. There is no max attempt limit in this version (see Areas for Improvement).
- **Push and pull jobs may overlap** — `[DisallowConcurrentExecution]` prevents each job from running in parallel with itself, but the push job and pull job may run concurrently. This is acceptable: they operate on different data sources (push reads `sync_events`; pull reads the external API).

### Infrastructure

- **Single instance** — the circuit breaker state and `[DisallowConcurrentExecution]` guard are per-process. In a multi-instance deployment, two instances could run the same job simultaneously. Acceptable for this challenge scope.
- **SQL Server** — the devcontainer provisions SQL Server. The `InMemory` EF Core provider is used only in unit tests.
- **Automatic migration on startup** — `db.Database.MigrateAsync()` runs at startup so the devcontainer works from scratch without manual `dotnet ef database update`. Acceptable for a challenge; production deployments would run migrations as a separate CI step.

---

## 7. Out of Scope

- **Versioning on local entities for concurrency control** — a `version` column was added to `TodoList` and `TodoItem` but optimistic locking (HTTP 409 on stale version) was not implemented. Documenting this as a known gap.
- **API versioning** — no `/v1/` prefix or `Accept: application/vnd.*` versioning was added to the local API.
- **Outbox Pattern** — the sync event log resembles an outbox but is not a formal Outbox Pattern implementation. There is no transactional relay, no message broker, and no guaranteed-delivery semantics. Events are written by handlers directly and processed by a polling job. The formal Outbox Pattern (with a dedicated relay and message bus) is out of scope.
- **Authentication on the external API** — the spec states no auth is required.
- **Automatic conflict resolution** — manual resolution required after fail-fast.
- **Multi-tenant / multi-external-system** — single external API instance only.

---

## 8. CorrelationId on outbound sync requests

Every `SyncEvent` carries a `CorrelationId` (`Guid`, assigned at
construction via `GuidV7.NewGuid()`). The outbound strategies send it
as the HTTP header `X-Correlation-Id` on every mutation call
(`POST /todolists`, `PATCH /todolists/{id}`, `DELETE /todolists/{id}`,
`PATCH /todolists/{id}/todoitems/{id}`, `DELETE /todolists/{id}/todoitems/{id}`).

**Why:** the header is stable across replays of the same `SyncEvent`.
`OutboundSyncJob` batches its `SaveChangesAsync` calls (see §9), so a
crash between a successful external call and the batch flush leaves
events marked `Pending` in the DB. On the next run they are
re-dispatched — same `CorrelationId`, same payload — and an external
API that honors the header can deduplicate. Without dedup, a
mid-batch crash produces duplicate external rows.

The external mock used in this repo does **not** honor the header —
it creates a new row on every POST. The convention is defensive
against production providers; in dev, a crash mid-batch can produce
duplicates in the mock's in-memory store.

The field is named `CorrelationId` locally for consistency with how
it grew out of the design conversation. Semantically it behaves like
the `Idempotency-Key` header popularized by Stripe; we send it as
`X-Correlation-Id` rather than `Idempotency-Key` to match the local
field name 1:1. If we need to integrate with a provider that
requires `Idempotency-Key` specifically, it is a one-line change in
the Refit interface — the local field name stays.

---

## 9. Batch-saving in OutboundSyncJob and InboundSyncJob

Both jobs flush `SaveChangesAsync` in configurable batches rather than after every item.

**OutboundSyncJob** flushes every `ProcessOptions.BatchSizeOutbound` processed events (default 10), plus a final flush for any remainder. The previous implementation flushed after every event — one DB round-trip per external API call.

**InboundSyncJob** applies the same pattern: flushes every `ProcessOptions.BatchSizeInbound` external lists processed (default 10), plus a final flush for any remainder. The previous implementation flushed after every external list.

**Trade-off:** if the process crashes after making external API
calls but before the batch flush, those events remain `Pending` in
the DB and will be re-dispatched on the next run. We accept this
because:

1. The external API is expected to dedupe on `X-Correlation-Id`
   (see §8). Replays are therefore at-most-once from the API's
   point of view.
2. The mock external API used in dev does **not** dedupe — in dev a
   crash mid-batch can produce duplicate external rows. This is an
   acknowledged trade against I/O cost; not a correctness concern
   when running against an idempotent-honoring production API.

Both properties live in `TodoApi/Infrastructure/Settings/ProcessOptions.cs`
and are bound in `ApplicationExtensions.AddApplication` from the
`Process` section of `appsettings.json`.

---

## 10. Requirements and Working Style Notes

Beyond the challenge brief, a small set of additional requirements was written prior to implementation to capture design decisions, constraints, and acceptance criteria. These live in `docs/req/` (REQ-001 through REQ-010) alongside `docs/EPIC.md`.

Not every piece of work went through a formal requirement — smaller or self-evident tasks were handled directly as they came up during development. The more complex features (sync module, bulk complete) were documented as full requirements and implementation prompts as an example of the working methodology: writing a REQ before touching code, keeping design decisions explicit, and generating scoped prompts for each implementation session.
