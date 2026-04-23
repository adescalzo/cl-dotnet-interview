# EPIC: Todo API Bidirectional Synchronization Module

**Epic ID:** EPIC-001  
**Date:** 2026-04-21  
**Status:** Defined  
**Source:** [Master Requirements](./01-master-requirements.md)

---

## Epic Summary

Build a synchronization module that keeps the local Todo API in sync with an external Todo API. The module uses a **sync event log** for outbound changes (push) and a **scheduled pull job** for inbound changes. Both jobs run on Quartz.NET with `[DisallowConcurrentExecution]`. The external API is accessed via **Refit**. Transient failures are handled by **Polly** retry policies.

---

## Business Justification

The local Todo API operates in isolation. The challenge requires extending it to interoperate with an external system. The sync module must be production-grade: resilient, observable, and safe — it must never silently lose data.

---

## Requirements Index

| Req ID | Title | Type | Priority |
|--------|-------|------|----------|
| REQ-001 | Sync Event Log | Functional | Critical |
| REQ-002 | Sync Identity Mapping | Functional | Critical |
| REQ-003 | Push Sync — Local to External | Functional | Critical |
| REQ-004 | Pull Sync — External to Local | Functional | Critical |
| REQ-005 | Deletion Handling (Both Directions) | Functional | Critical |
| REQ-006 | Resilience with Polly | Non-Functional | Critical |
| REQ-007 | Refit External API Client | Functional | Critical |
| REQ-008 | Quartz Scheduling | Functional | High |
| REQ-009 | Testing Requirements | Non-Functional | Required |
| REQ-010 | Bulk Complete All Items | Functional | High |
| REQ-011 | Sync Status Notification in Footer | Functional | Medium |

---

## Acceptance Criteria (Epic Level)

1. Running a sync cycle with an empty local DB and populated external API creates all external lists and items locally.
2. Running a sync cycle after local changes reflects those changes in the external API.
3. A conflict (both sides changed since last sync) is logged, both sides left unmodified, cycle continues.
4. Deleting a list locally causes it to be deleted from the external API on the next push cycle.
5. Deleting a list externally causes it to be removed locally on the next pull cycle.
6. A sync that partially fails does not corrupt already-synced records and retries on the next cycle.
7. `GET /todolists` is called exactly once per pull cycle.
8. A 503 from the external API triggers Polly retries; a 422 does not.
9. Both jobs refuse to run in parallel — second trigger skips until the first finishes.
10. All sync operations produce structured Serilog log output with entity IDs, operation type, and outcome.
11. All test scenarios in REQ-009 pass with a single `dotnet test`.

---

## Technical Boundaries

**In scope:** Sync event log, sync mapping persistence, push job, pull job, conflict detection (fail-fast, log and skip), Refit client, Polly resilience, Quartz scheduling, SignalR broadcast, structured logging.

**Out of scope:** Authentication on the external API, UI for triggering sync, real-time sync (webhooks), automatic conflict resolution, multi-tenant.

**Key constraint:** No individual `POST /todoitems` endpoint on the external API — items can only be created during `POST /todolists`. New items on already-synced lists require an explicit strategy (documented in NOTES.md).

**Key constraint:** `source_id` is write-once on the external API — it cannot be patched after creation.

**Key constraint:** No `version` field on external API — use `updated_at` as proxy for external change detection.

---

## Technology Stack (Sync Module)

| Concern | Library | Notes |
|---|---|---|
| Scheduling | `Quartz.Extensions.Hosting` | ADR-0012 |
| External HTTP client | `Refit` + `Refit.HttpClientFactory` | REQ-007 |
| Resilience / retry | `Polly.Extensions.Http` | REQ-006 |
| Real-time broadcast | `Microsoft.AspNetCore.SignalR` (existing `NotificationHub`) | ADR-0012 |
| Persistence | EF Core (existing, ADR-0005) | — |
| Logging | Serilog (existing, ADR-0009) | — |

---

## Companion Documents

- [requirements/](./requirements/) — Individual requirement specifications
- [03-work-plan.md](./03-work-plan.md) — Phased implementation tasks
- [04-suggested-api-improvements.md](./04-suggested-api-improvements.md) — Improvements proposed to the external API
