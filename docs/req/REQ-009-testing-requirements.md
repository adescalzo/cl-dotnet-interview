# REQ-009: Testing Requirements

**Epic:** EPIC-001  
**Type:** Non-Functional — Required

---

## Requirement

Automated tests must cover all sync scenarios. Tests run with a single `dotnet test` command. Test conventions follow ADR-0011 (xUnit, FluentAssertions, NSubstitute, Bogus).

---

## Specification

### 1. Test strategy

| Layer | Approach |
|---|---|
| Repository tests | Real EF Core InMemory provider. Per-test database (Guid name). |
| Job unit tests | NSubstitute mocks for `IExternalTodoApiClient`, `ISyncEventRepository`, `ISyncMappingRepository`. No real HTTP calls. |
| Handler + event enqueue | Real EF Core InMemory, real handler, verify event row written in same transaction. |
| Refit client | Integration test against a mock HTTP server (e.g. `WireMock.Net`) or a controllable test instance of the external API. |

### 2. Required scenarios

#### Sync Event Log (REQ-001)

- [ ] `CreateTodoListHandler` commit → one `Pending` event with correct fields
- [ ] `DeleteTodoListHandler` commit → one `Deleted` event for list, zero for items
- [ ] Transaction rollback → no event persisted

#### Sync Mapping (REQ-002)

- [ ] `FindByLocalId` returns correct mapping
- [ ] `FindByExternalId` returns correct mapping
- [ ] Upsert updates `ExternalUpdatedAt` and `LastSyncedAt`
- [ ] Delete removes mapping; subsequent lookup returns null

#### Push Job (REQ-003)

- [ ] `Created` event (list with items) → `POST` called with `source_id`. Mapping created.
- [ ] `Updated` event → `PATCH` called. `ExternalUpdatedAt` updated.
- [ ] Coalescing: 3 `Updated` events for same entity → 1 `PATCH` call.
- [ ] Conflict (external `updated_at` advanced) → no PATCH, event `Failed`, CONFLICT logged.
- [ ] `Deleted` list event → `DELETE` called. No item delete calls. Mapping removed.
- [ ] Duplicate `Created` (mapping already exists) → skipped, `Completed`.
- [ ] `[DisallowConcurrentExecution]`: two triggers → second skips (verify via Quartz test harness or job flag).

#### Pull Job (REQ-004)

- [ ] Empty local DB + 2 external lists with items → 2 local lists + items created. Mappings stored.
- [ ] Known external list, `updated_at` unchanged → no modification.
- [ ] Known external list, `updated_at` advanced → CONFLICT logged. No local write.
- [ ] External list absent from GET, mapping exists → local list deleted. Mapping removed.
- [ ] GET failure → no local writes. Cycle aborts.
- [ ] New local records from pull → zero `SyncEvent` rows inserted.

#### Deletion handling (REQ-005)

- [ ] 404 on `DELETE /todolists/{id}` → treated as success. Event `Completed`. Mapping removed.
- [ ] 404 on `DELETE /todoitems/{id}` → same.
- [ ] No mapping for delete event → `Completed` without API call.

#### Resilience (REQ-006)

- [ ] 503 → Polly retries 3 times with increasing delay. After 3 failures → event `Failed`. Cycle continues.
- [ ] 422 → not retried. Immediate failure.
- [ ] One entity fails → subsequent entities still processed.

#### Refit client (REQ-007)

- [ ] `GetAllAsync` deserializes snake_case JSON correctly.
- [ ] `CreateTodoListAsync` sends `source_id` in request body.
- [ ] `DeleteTodoListAsync` with 404 → no exception thrown.

### 3. Test data builders

Follow ADR-0011: use `Bogus`-backed immutable `IBuilder<T>` for `SyncEvent`, `SyncMapping`, `ExternalTodoList`, `ExternalTodoItem`.

---

## Acceptance Criteria

- [ ] `dotnet test` runs all tests from repo root without additional setup.
- [ ] All scenarios listed in Section 2 have at least one test.
- [ ] No test calls the real external API (mock or stub for all HTTP).
- [ ] No test shares state with another test (InMemory DB uses unique name per test).
