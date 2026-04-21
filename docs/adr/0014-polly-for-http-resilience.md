# 0014 - Polly for HTTP resilience on external API calls

- Status: accepted
- Date: 2026-04-21
- Deciders: TodoApi team

## Context and problem statement

External API calls in the sync jobs are susceptible to transient
failures: network blips, 503 overload responses, 429 rate limiting.
These failures should be retried automatically so that a single
flaky request does not mark an entity as permanently failed.

At the same time, the retry policy must be bounded — unbounded retries
block the job and delay other entities. A circuit breaker prevents the
job from hammering a clearly unavailable external service.

Polly policies attach cleanly to `IHttpClientFactory` pipelines (ADR-0013),
keeping all resilience logic outside application and job code.

## Decision drivers

- Retry logic must not live in job or application code — jobs must stay
  focused on sync orchestration, not HTTP mechanics.
- Retry should use exponential backoff with jitter to avoid
  thundering-herd when many entities fail at once.
- 4xx errors (except 429) are not retryable — they indicate a permanent
  problem with the request, not the server.
- A circuit breaker protects the external API from being flooded when it
  is clearly down.
- Max retry count must be configurable without recompiling.
- Per-entity failures must be isolated — a single entity exhausting
  retries must not abort the cycle (this is a job-level concern, but
  the retry policy must be bounded so the job can move on).

## Considered options

- **Polly v8 (`Microsoft.Extensions.Resilience` / `Polly.Extensions.Http`)**
  — first-party .NET resilience library; `IHttpClientFactory` integration
  via `.AddPolicyHandler()`; well-established; strategy composition.
- **Custom retry loop inside jobs** — full control; no dependency; retry
  logic scattered across multiple jobs; not testable in isolation.
- **`HttpClient` with `RetryHandler`** — manual `DelegatingHandler`;
  same result as Polly with more boilerplate and no strategy composition.

## Decision outcome

Chosen option: **Polly (`Polly.Extensions.Http`)**, policies attached to
the Refit `HttpClient` registration (ADR-0013) via `.AddPolicyHandler()`.

---

### Conventions

#### 1. Package

Add to `Directory.Packages.props`:

```xml
<PackageVersion Include="Microsoft.Extensions.Http.Polly" Version="8.*" />
```

> Use `Microsoft.Extensions.Http.Polly` (the `IHttpClientFactory`
> integration package). It pulls in `Polly` transitively.

#### 2. Retry policy

Defined as a static factory in `Infrastructure/Configuration/ResiliencePolicies.cs`:

```csharp
public static class ResiliencePolicies
{
    public static IAsyncPolicy<HttpResponseMessage> RetryPolicy(
        int maxRetries = 3)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()          // HttpRequestException + 5xx + 408
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                sleepDurationProvider: (attempt, outcome, _) =>
                    TimeSpan.FromSeconds(Math.Pow(2, attempt))
                    + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                onRetryAsync: (outcome, delay, attempt, _) =>
                {
                    Log.Warning(
                        "Retry {Attempt}/{Max} for {Method} {Uri} — " +
                        "{Status} — waiting {DelayMs}ms",
                        attempt, maxRetries,
                        outcome.Result?.RequestMessage?.Method,
                        outcome.Result?.RequestMessage?.RequestUri,
                        outcome.Result?.StatusCode ?? (object)outcome.Exception?.GetType().Name!,
                        (int)delay.TotalMilliseconds);
                    return Task.CompletedTask;
                });
    }
```

`HttpPolicyExtensions.HandleTransientHttpError()` covers:
- `HttpRequestException` (network failure, DNS, timeout)
- HTTP 5xx
- HTTP 408 (Request Timeout)

The extra `.OrResult(r => r.StatusCode == TooManyRequests)` adds 429.

**Not retried:** 4xx other than 408 and 429. These are permanent — a
malformed request will still be malformed after 3 attempts.

#### 3. Backoff strategy

| Attempt | Base delay | Jitter range | Approx total |
|---|---|---|---|
| 1st retry | 2 s | 0–1 s | ~2–3 s |
| 2nd retry | 4 s | 0–1 s | ~4–5 s |
| 3rd retry | 8 s | 0–1 s | ~8–9 s |
| Give up | — | — | — |

Jitter (`Random.Shared.Next(0, 1000)` ms) prevents synchronized retries
from multiple concurrent requests hitting the external API simultaneously.

Max retries loaded from `appsettings` in the registration:

```csharp
var maxRetries = configuration.GetValue("ExternalApi:MaxRetries", defaultValue: 3);
.AddPolicyHandler(_ => ResiliencePolicies.RetryPolicy(maxRetries))
```

#### 4. Circuit breaker policy

```csharp
    public static IAsyncPolicy<HttpResponseMessage> CircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (_, duration) =>
                    Log.Warning("Circuit breaker OPEN for {Duration}s", duration.TotalSeconds),
                onReset: () =>
                    Log.Information("Circuit breaker CLOSED"),
                onHalfOpen: () =>
                    Log.Information("Circuit breaker HALF-OPEN — probing"));
```

When the circuit is open, Polly throws `BrokenCircuitException`
immediately without making the HTTP call. The per-entity catch in the
job treats this the same as an exhausted retry — marks the event
`Failed` and continues the cycle.

#### 5. Policy attachment order

Attach **retry first, then circuit breaker** (outermost to innermost):

```csharp
services
    .AddRefitClient<IExternalTodoApiClient>(refitSettings)
    .ConfigureHttpClient(...)
    .AddPolicyHandler(ResiliencePolicies.RetryPolicy(maxRetries))    // outer
    .AddPolicyHandler(ResiliencePolicies.CircuitBreakerPolicy());    // inner
```

Order matters: the retry policy wraps the circuit breaker. Each retry
attempt passes through the circuit breaker, so a circuit-open state
stops the retry loop immediately rather than retrying a broken circuit.

#### 6. Timeout: HttpClient vs. Polly

- `HttpClient.Timeout = 20s` — the absolute outer timeout for the
  entire operation including all retries. Acts as a safety net.
- Polly does not add a per-attempt timeout by default. If needed, add a
  `TimeoutPolicy` per attempt (inner) and a total timeout (outer). For
  now the `HttpClient.Timeout` is sufficient.

#### 7. Non-retryable error handling

When Polly exhausts retries or the circuit is open, it throws from the
Refit call. The per-entity `try/catch` in the job catches this,
marks the `SyncEvent` as `Failed`, logs `ERROR`, and continues:

```csharp
catch (Exception ex) when (ex is ApiException or BrokenCircuitException
                               or TaskCanceledException)
{
    logger.LogError(ex,
        "Failed {EntityType} {EntityId} after retries",
        evt.EntityType, evt.EntityId);
    await syncEventRepo.MarkFailedAsync(evt.Id, ex.Message, ct);
}
```

The job never re-throws from within the per-entity loop.

#### 8. GET /todolists in the pull job is NOT per-entity

The pull job's `GET /todolists` call is outside the per-entity loop.
If it fails after all retries, the exception propagates out of the job
method — the cycle aborts. Quartz logs the failure. No local writes
occur. This is the correct behavior: treating a GET failure as "no
external records" would cascade phantom deletions.

### Consequences

- Positive: retry and circuit-breaker logic lives in one file
  (`ResiliencePolicies.cs`), not scattered across jobs or services.
- Positive: policies attach at the `HttpClient` pipeline level — jobs
  and the Refit interface have zero awareness of resilience logic.
- Positive: Serilog WARN on each retry and INFO/WARN on circuit
  transitions gives full observability without extra instrumentation.
- Positive: `BrokenCircuitException` is a well-known type; the
  per-entity catch can handle it explicitly.
- Negative: the circuit breaker state is per-`HttpClient` instance
  (per-process). In a multi-instance deployment, each instance has its
  own circuit. Acceptable for a single-instance sync job.
- Negative: `Random.Shared` jitter is not crypto-random. Irrelevant for
  HTTP retry jitter.
- Neutral: `Microsoft.Extensions.Http.Polly` is the v7 integration
  package. Polly v8 introduces `ResiliencePipelineBuilder` with a
  different API. We use v7 (`WaitAndRetryAsync`, `CircuitBreakerAsync`)
  because it aligns with the ASP.NET Core 8 ecosystem. If we upgrade to
  .NET 9+, migrate to `AddResilienceHandler` (Polly v8).

## Links

- Builds on: ADR-0013 (Refit client — policies attach to its `HttpClient`).
- Builds on: ADR-0012 (Quartz jobs — per-entity catch consumes Polly exceptions).
- Library: <https://github.com/App-vNext/Polly>
- Integration: <https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-http-call-retries-exponential-backoff-polly>
