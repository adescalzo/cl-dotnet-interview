# REQ-011: Sync Status Notification in Footer

**Epic:** EPIC-001  
**Type:** Functional — Real-time UX  
**Depends on:** REQ-003 (OutboundSyncJob SignalR broadcast), REQ-004 (InboundSyncJob SignalR broadcast)

---

## Problem Statement

The frontend has no visibility into sync activity. When a sync cycle completes, the user gets no feedback. Adding a brief footer message on each sync event makes the real-time integration visible without requiring any new backend work.

---

## Requirement

When a SignalR sync event is received, display a short message in the app footer indicating the sync type and its outcome. The message auto-dismisses after a few seconds.

---

## Specification

### 1. Existing SignalR contract (backend already emits these)

| Method | Payload | Meaning |
|---|---|---|
| `OutboundSyncJob` | `{ processed: number, failed: number }` | Push cycle completed |
| `InboundSyncJob` | `{ synced: number }` | Pull cycle completed |

No backend changes required.

### 2. Footer layout

Add `<Footer>` from Ant Design (`Layout.Footer`) to `App.tsx`, below `<Content>`. The footer is always present but shows content only when a sync notification is active.

Suggested style: minimal height, centered text, light background — unobtrusive.

### 3. Notification content

| Event | Message |
|---|---|
| `OutboundSyncJob` (no failures) | `↑ Outbound sync complete — {processed} item(s) pushed` |
| `OutboundSyncJob` (with failures) | `↑ Outbound sync complete — {processed} pushed, {failed} failed` |
| `InboundSyncJob` | `↓ Inbound sync complete — {synced} item(s) received` |

If `processed = 0` and `failed = 0` (outbound idle cycle), no message is shown — avoid noise from empty cycles.

### 4. Dismissal

The message disappears automatically after **4 seconds**. A new event resets the timer and replaces the current message. No manual dismiss button needed.

### 5. Implementation location

A `useSyncNotification` hook (or inline in `App.tsx`) subscribes to both SignalR methods and manages a `{ message: string | null }` state. The footer renders `message` when non-null.

---

## Acceptance Criteria

- [ ] After an outbound sync cycle with pushes: footer shows `↑ Outbound sync complete — N item(s) pushed` and disappears after 4 s.
- [ ] After an outbound cycle with failures: footer shows failure count alongside pushed count.
- [ ] After an inbound sync cycle: footer shows `↓ Inbound sync complete — N item(s) received`.
- [ ] Empty outbound cycle (`processed = 0, failed = 0`): no footer message appears.
- [ ] Two sync events in quick succession: second message replaces the first; timer resets.
- [ ] Footer is present in all routes (lists page and detail page).
