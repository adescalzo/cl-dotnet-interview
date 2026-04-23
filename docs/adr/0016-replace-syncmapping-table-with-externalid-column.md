# 0016 - Replace SyncMapping table with ExternalId column on aggregates

- Status: accepted
- Date: 2026-04-23
- Deciders: adescalzo

## Context and problem statement

The sync module originally modeled local↔external id correlation with a
dedicated `SyncMapping` table (`EntityType`, `LocalId`, `ExternalId`,
`ExternalUpdatedAt`, `LastSyncedAt`). Every outbound strategy wrote a
parallel mapping row; `InboundSyncJob` looked up mappings by external id
to decide insert vs update. Outbound strategies also used the mapping
to check idempotency ("skip if we already synced this") — a check that
at one point got inverted and silently stopped the happy path.

Two observations collapsed this design:

1. The external API (`TodoStore.FindList`) resolves lookups by **either**
   the external `Id` **or** the caller-supplied `SourceId` (our local
   `Guid.ToString()`). For entities we own, outbound update/delete do
   not need a translation table — `payload.Id.ToString()` works as the
   URL parameter.
2. The only hard requirement for a correlation store is the
   externally-injected case (inbound payloads with `SourceId == null`,
   which the mock produces on every `GET /todolists` via random
   injection). That correlation is 1:1 with the local aggregate, so a
   column on the aggregate models it without a junction table.

The right shape for outbound idempotency turned out to be
`SyncEvent.Status` — the `OutboundSyncJob` filter already picks up only
`Pending` events, so the mapping-exists check was redundant from the
start.

## Decision drivers

- Eliminate dual-write consistency problems (strategy writes to the
  external API, then writes a `SyncMapping` row — two writes, two
  failure modes, no transaction spanning them).
- One aggregate, one row. Correlation metadata travels with the
  entity.
- Fewer moving parts: delete the entity, repository, interface, EF
  configuration, DbSet, DI registration, and test-support double.
- Strategies become payload-in / HTTP-out, with no aggregate access.
  Idempotency is `SyncEvent.Status` plus a `CorrelationId` header on
  each outbound call — the external API dedupes on replays, we do
  not.

## Considered options

- **A.** Keep `SyncMapping` table.
- **B.** Nullable `ExternalId` string column on `TodoList` and
  `TodoItem`.
- **C.** Value-object `ExternalReference(ExternalId, ExternalUpdatedAt,
  LastSyncedAt)` owned by the aggregate.

## Decision outcome

Chosen option: **B**, because it models a 1:1 relationship without a
junction table, keeps correlation metadata with the aggregate, and
deletes the most code. `ExternalUpdatedAt` / `LastSyncedAt` fields
from the mapping are dropped: they were only read for delta-skip in
`InboundSyncJob`, and the current design does a full upsert on every
poll. When delta-cursoring is needed, a per-collection `SyncCursor`
table is the right shape, not per-entity columns.

Option C is the right shape if this grows additional correlation
metadata (e.g. provider, etag, retry counters). Until there is a
second field, it is one field dressed as a value object.

### Consequences

- Positive: strategies shrink to one I/O per use case; inbound job
  reads a single column; the branch's migration becomes a single
  `ExternalId` column on each aggregate and the regenerated
  `SyncEvent` table.
- Negative: losing `ExternalUpdatedAt` / `LastSyncedAt` removes the
  delta-skip path in `InboundSyncJob`. Every poll now writes through
  every changed field. Acceptable while external is source of truth
  and local edits to synced fields are not supported.
- Neutral: `ExternalId` is nullable — lists that have never been
  synced, or lists created locally before sync ran, have no external
  correlation. Strategies that need one return early.

## Links

- Supersedes: the storage aspect of ADR-0015. Strategy pattern for
  dispatch is retained.
- ADR-0013 (Refit client contract — the external `Id`/`SourceId`
  semantics this decision relies on).
