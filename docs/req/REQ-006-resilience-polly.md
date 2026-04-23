# REQ-006: Resilience with Polly

**Epic:** EPIC-001  
**Type:** Non-Functional — Critical  
**Depends on:** REQ-007 (Refit Client)

---

## Problem Statement

External API calls can fail transiently (network issues, 5xx, 429). These should be retried automatically. Individual entity failures must not abort the entire sync cycle. The GET failure case in the pull job must abort the cycle to prevent phantom deletions.

---

## Requirement

Use **Polly** (`Polly.Extensions.Http`) attached to the Refit `HttpClient` for retry logic. Wrap per-entity processing in both jobs with isolation so one failure does not stop the cycle.

---

## Specification

### 1. Polly retry policy

Attach to the Refit `HttpClient` via `.AddPolicyHandler(...)` in `Infrastructure/Configuration/`.

**Retryable conditions:**
- HTTP 429 (Too Many Requests)
- HTTP 500, 502, 503, 504
- `HttpRequestException` (timeout, connection refused)

**Non-retryable conditions:**
- HTTP 4xx (except 429) — these are permanent failures; retry won't help.

**Strategy:** Exponential backoff with decorrelated jitter (Polly `WaitAndRetryAsync`).

```csharp
Policy
  .Handle<HttpRequestException>()
  .OrResult<HttpResponseMessage>(r =>
      r.StatusCode == HttpStatusCode.TooManyRequests ||
      (int)r.StatusCode >= 500)
  .WaitAndRetryAsync(
      retryCount: 3,                             // configurable via appsettings
      sleepDurationProvider: attempt =>
          TimeSpan.FromSeconds(Math.Pow(2, attempt))
          + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
      onRetry: (outcome, delay, attempt, context) =>
          logger.LogWarning("Retry {Attempt}/3 for {Operation} — waiting {Delay}ms",
              attempt, context.OperationKey, delay.TotalMilliseconds));
```

Max retries configurable via `appsettings` key `ExternalApi:MaxRetries` (default 3).

### 2. Circuit breaker (recommended)

Optional but recommended: open the circuit after 5 consecutive failures, half-open after 30 seconds.

```csharp
Policy
  .Handle<HttpRequestException>()
  .OrResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500)
  .CircuitBreakerAsync(
      handledEventsAllowedBeforeBreaking: 5,
      durationOfBreak: TimeSpan.FromSeconds(30));
```

### 3. Per-entity isolation in jobs

In `OutboundSyncJob` and `InboundSyncJob`, each entity is processed in a `try/catch`:

```csharp
foreach (var entity in entities)
{
    try { /* process */ }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed processing {EntityType} {EntityId}", entity.EntityType, entity.EntityId);
        // mark event Failed; continue
    }
}
```

One entity's failure must not stop the loop.

### 4. GET failure in InboundSyncJob aborts cycle

`GET /todolists` in `InboundSyncJob` is **not** wrapped in the per-entity catch. If it throws after all Polly retries → the job method throws → Quartz logs the error → cycle ends. No local writes occur after a failed GET (phantom deletion guard).

### 5. Retry logging

Each retry attempt must produce a WARN log:
```
[WARN] Retry 1/3 for PATCH /todolists/abc — HTTP 503 — waiting 2341ms
[ERROR] Exhausted retries for PATCH /todolists/abc — HTTP 503
```

---

## Acceptance Criteria

- [ ] 503 response triggers Polly retry up to configured max. Delay increases between attempts.
- [ ] 422 response does not trigger a retry.
- [ ] After exhausting retries, the entity is marked `Failed` and the cycle continues for other entities.
- [ ] GET failure in pull job → no local writes. Cycle aborts. Error logged.
- [ ] Each retry produces a WARN log with attempt number, endpoint, status, and delay.
- [ ] Max retry count is configurable via `appsettings`.
