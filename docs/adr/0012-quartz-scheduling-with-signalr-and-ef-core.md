# 0012 - Background scheduling with Quartz.NET, SignalR monitoring, and scoped EF Core

- Status: accepted
- Date: 2026-04-21
- Deciders: TodoApi team

## Context and problem statement

We need to execute recurring background work (e.g. syncing data,
housekeeping, notifications) on a schedule. Jobs need access to the
persistence layer (EF Core + repositories per ADR-0005) and must
broadcast progress to connected clients in real time. The existing
`NotificationHub` (SignalR) already provides that broadcast channel.

The main technical constraint is that `DbContext` is registered as
**scoped**, but Quartz jobs are resolved as **singletons** by default.
Injecting a scoped service directly into a singleton leaks the scope
and corrupts EF Core's change tracker across executions.

## Decision drivers

- Scheduled jobs must not share state across executions.
- Jobs must reach the same repository abstractions that Wolverine
  handlers use (ADR-0005), not bypass them by injecting `DbContext`
  directly.
- Real-time progress must flow through the existing `NotificationHub`
  without introducing a second notification channel.
- Concurrent vs. non-concurrent execution must be declarative, not
  conditional logic.
- The scheduler must start and stop cleanly with the host lifecycle
  (`IHostedService`).

## Considered options

- **Quartz.NET** — mature .NET scheduler, `IHostedService` integration,
  `[DisallowConcurrentExecution]` attribute, first-party DI integration
  via `Quartz.Extensions.DependencyInjection`.
- **Hangfire** — popular alternative; requires a backing store
  (SQL Server / Redis) even for simple recurring jobs; heavier
  operational footprint.
- **`System.Threading.PeriodicTimer` + `BackgroundService`** — no
  scheduler UI, no persistence, no concurrency controls. Sufficient
  for a single job but does not scale to multiple independent schedules.

## Decision outcome

Chosen option: **Quartz.NET** (`Quartz.Extensions.Hosting`).

---

### Conventions

#### 1. Job registration

Register jobs and triggers in `Infrastructure/Configuration/` via an
extension method, keeping `Program.cs` free of scheduler details:

```csharp
// Infrastructure/Configuration/QuartzExtensions.cs
public static class QuartzExtensions
{
    public static IServiceCollection AddQuartzScheduler(
        this IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            q.AddJobAndTrigger<TodoListSyncJob>(
                identity: "TodoListSyncJob",
                cronExpression: "0/30 * * * * ?");   // every 30 s

            q.AddJobAndTrigger<CleanupJob>(
                identity: "CleanupJob",
                cronExpression: "0 0 3 * * ?");       // daily at 03:00
        });

        services.AddQuartzHostedService(opt =>
            opt.WaitForJobsToComplete = true);

        return services;
    }

    private static void AddJobAndTrigger<TJob>(
        this IServiceCollectionQuartzConfigurator q,
        string identity,
        string cronExpression)
        where TJob : IJob
    {
        var key = new JobKey(identity);
        q.AddJob<TJob>(opts => opts.WithIdentity(key));
        q.AddTrigger(opts => opts
            .ForJob(key)
            .WithIdentity($"{identity}-trigger")
            .WithCronSchedule(cronExpression));
    }
}
```

Call it from `Program.cs`:

```csharp
builder.Services.AddQuartzScheduler();
```

---

#### 2. Scope management inside jobs

Jobs **must not** inject `DbContext`, repositories, or the unit of work
directly. Use `IServiceScopeFactory` to create a per-execution scope:

```csharp
// Application/Jobs/TodoListSyncJob.cs
[DisallowConcurrentExecution]           // remove for concurrent jobs
public sealed class TodoListSyncJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<NotificationHub> _hub;

    public TodoListSyncJob(
        IServiceScopeFactory scopeFactory,
        IHubContext<NotificationHub> hub)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Business logic via repository — not DbContext directly
        var lists = await uow.TodoLists.GetAllAsync(context.CancellationToken);

        foreach (var list in lists)
        {
            // ... do work ...
            await _hub.Clients.All.SendAsync(
                "JobProgress",
                new { Job = nameof(TodoListSyncJob), ListId = list.Id },
                context.CancellationToken);
        }
    }
}
```

`IHubContext<T>` is a **singleton** — safe to inject directly.
`IServiceScopeFactory` is also a **singleton** — safe to inject
directly. Never inject `IUnitOfWork`, repositories, or `DbContext`
as constructor parameters on a job class.

---

#### 3. Concurrent vs. non-concurrent execution

| Attribute | Behavior |
|---|---|
| `[DisallowConcurrentExecution]` | Second trigger fires only after current execution completes. Queued. |
| _(no attribute)_ | Each trigger fires a new instance regardless of in-flight executions. |

Choose based on the job's idempotency:
- **Idempotent / read-only** jobs (reporting, monitoring) → concurrent.
- **Write / stateful** jobs (sync, cleanup) → `[DisallowConcurrentExecution]`.

---

#### 4. SignalR channel naming

Each job broadcasts on a named client method so consumers can subscribe
selectively. Use `PascalCase` matching the job name:

| Job class | SignalR method |
|---|---|
| `TodoListSyncJob` | `"TodoListSyncJob"` |
| `CleanupJob` | `"CleanupJob"` |

Re-use the existing `NotificationHub` — do not create a second hub.

---

#### 5. File placement

```
TodoApi/
  Application/
    Jobs/
      TodoListSyncJob.cs       ← IJob implementation
      CleanupJob.cs
  Infrastructure/
    Configuration/
      QuartzExtensions.cs      ← DI registration
```

Jobs live in `Application/Jobs/` because they orchestrate domain work
(same layer as command handlers). They depend on repository interfaces
from `Domain`, not on EF Core types from `Infrastructure`.

---

### Consequences

- Positive: `IServiceScopeFactory` pattern eliminates the scope-leak
  problem without fighting EF Core's lifetime assumptions.
- Positive: `[DisallowConcurrentExecution]` makes concurrency policy
  explicit and readable — no runtime locking or flags.
- Positive: re-using `NotificationHub` and `IUnitOfWork` keeps the
  new surface area small; no new interfaces required.
- Negative: each job execution creates and disposes a DI scope. This
  is the correct pattern but adds one allocation per execution — not
  a concern at the cadences we expect.
- Negative: Quartz introduces its own in-memory scheduler state. If the
  host restarts mid-job, that execution is lost. Persistent stores
  (Quartz + SQL Server) are available if durable scheduling becomes
  a requirement; that would warrant a separate ADR.
- Neutral: jobs bypass the Wolverine pipeline (no validation
  middleware, no transaction middleware). Jobs must call
  `uow.SaveChangesAsync()` explicitly and handle their own errors.

## Links

- Builds on: ADR-0005 (EF Core, `IUnitOfWork`, per-module `DbContext`).
- Related: ADR-0008 (Wolverine — jobs are outside the Wolverine pipeline).
- Related: ADR-0009 (Serilog — use `ILogger<T>` inside jobs, same as everywhere else).
- Reference: <https://damienbod.com/2021/11/08/asp-net-core-scheduling-with-quartz-net-and-signalr-monitoring/>
- Library: <https://www.quartz-scheduler.net/>
