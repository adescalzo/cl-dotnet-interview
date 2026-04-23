# REQ-002: Sync Identity Mapping

**Epic:** EPIC-001  
**Type:** Functional — Critical

---

## Problem Statement

The local system and the external API use different ID spaces. To PATCH or DELETE an external record, the push job must resolve the local ID to an external ID. To detect conflicts on pull, the system must know the last external `updated_at` it observed for a given record.

---

## Requirement

The system must maintain a `sync_mappings` table that links local IDs to external IDs and stores the last-known external timestamp per record.

---

## Specification

### 1. Schema

| Field | Type | Description |
|---|---|---|
| `Id` | Guid (UUIDv7) | Primary key |
| `EntityType` | enum | `TodoList` or `TodoItem` |
| `LocalId` | Guid | Local primary key of the entity |
| `ExternalId` | string | ID assigned by the external API |
| `ExternalUpdatedAt` | DateTime UTC | The `updated_at` value from the external API at the time of last sync — used to detect external changes |
| `LastSyncedAt` | DateTime UTC | When this mapping was last written |

**Indexes required:**
- `(EntityType, LocalId)` — local → external lookup (unique)
- `(EntityType, ExternalId)` — external → local lookup (unique)

### 2. source_id population on push

When creating a record on the external API (`POST /todolists`), the request body must include `source_id` set to the local entity's ID (as a string). This allows the external system to record the origin.

> **Constraint:** `source_id` is write-once. `UpdateTodoListBody` and `UpdateTodoItemBody` do not accept `source_id`. Never attempt to PATCH it.

### 3. Mapping lifecycle

| Event | Action |
|---|---|
| Local record pushed and created externally | Insert mapping with `ExternalId` and `ExternalUpdatedAt` |
| External record pulled and created locally | Insert mapping with `ExternalId` and `ExternalUpdatedAt` |
| Push or pull update succeeds | Update `ExternalUpdatedAt` and `LastSyncedAt` |
| Local record deleted (push delete succeeds) | Delete mapping |
| External record absent from GET (inbound deletion) | Delete mapping after deleting local record |

### 4. Repository interface

```csharp
Task<SyncMapping?> FindByLocalIdAsync(EntityType type, Guid localId, CancellationToken ct);
Task<SyncMapping?> FindByExternalIdAsync(EntityType type, string externalId, CancellationToken ct);
Task UpsertAsync(SyncMapping mapping, CancellationToken ct);
Task DeleteByLocalIdAsync(EntityType type, Guid localId, CancellationToken ct);
Task<IReadOnlyList<SyncMapping>> GetAllByTypeAsync(EntityType type, CancellationToken ct);
```

### 5. Conflict detection using ExternalUpdatedAt

Before pushing an update (PATCH), the push job compares:
- `mapping.ExternalUpdatedAt` — what we last saw externally
- `externalRecord.updated_at` — what the external system reports now (from the cached GET response)

If `externalRecord.updated_at > mapping.ExternalUpdatedAt` → external changed since last sync → **conflict**. Log and skip; do not PATCH.

---

## Acceptance Criteria

- [ ] After pushing a new list, a mapping exists with `LocalId`, `ExternalId`, and `ExternalUpdatedAt` matching the external API's response.
- [ ] After pulling a new external list, a mapping is created with the external record's `updated_at` as `ExternalUpdatedAt`.
- [ ] `FindByLocalId` returns the mapping without an additional API call.
- [ ] `FindByExternalId` returns the mapping without an additional API call.
- [ ] After a successful push update, `ExternalUpdatedAt` is updated to the new external `updated_at`.
- [ ] Deleting a local record and completing the external delete removes the mapping.

---

## Constraints

- External records with `source_id = null` are valid — they are unlinked external records that should be imported on pull.
- `source_id` on the external API is a string. Cast local Guid to string when sending.
