# REQ-008: Quartz Scheduling

**Epic:** EPIC-001  
**Type:** Functional — High  
**See also:** ADR-0012 (Quartz.NET + SignalR + EF Core)

---

## Problem Statement

Both sync jobs need to run on a schedule without human intervention, must not execute in parallel with themselves, and must start and stop cleanly with the application host.

---

## Requirement

Register both sync jobs with Quartz.NET (`Quartz.Extensions.Hosting`) as `IHostedService`. Each job uses `[DisallowConcurrentExecution]`. Schedules are configurable via `appsettings`.

---

## Specification

### 1. Jobs and default schedules

| Job | Class | Default schedule | Concurrency |
|---|---|---|---|
| Push (outbound) | `OutboundSyncJob` | Every 1 minute | `[DisallowConcurrentExecution]` |
| Pull (inbound) | `InboundSyncJob` | Every 5 minutes | `[DisallowConcurrentExecution]` |

### 2. Registration

In `Infrastructure/Configuration/QuartzExtensions.cs` (ADR-0012 pattern):

```csharp
services.AddQuartz(q =>
{
    q.AddJobAndTrigger<OutboundSyncJob>(
        config["Quartz:OutboundSyncCron"] ?? "0 0/1 * * * ?");
    q.AddJobAndTrigger<InboundSyncJob>(
        config["Quartz:InboundSyncCron"] ?? "0 0/5 * * * ?");
});
services.AddQuartzHostedService(opt => opt.WaitForJobsToComplete = true);
```

### 3. EF Core scope inside jobs

Per ADR-0012, jobs must not inject `DbContext` or repositories directly. Use `IServiceScopeFactory` to create a per-execution scope:

```csharp
public async Task Execute(IJobExecutionContext context)
{
    await using var scope = _scopeFactory.CreateAsyncScope();
    var syncEventRepo = scope.ServiceProvider.GetRequiredService<ISyncEventRepository>();
    // ...
}
```

### 4. SignalR via IHubContext

`IHubContext<NotificationHub>` is a singleton — inject directly into job constructor, not inside the scope.

### 5. Configuration keys

```json
"Quartz": {
  "OutboundSyncCron": "0 0/1 * * * ?",
  "InboundSyncCron": "0 0/5 * * * ?"
}
```

### 6. Graceful shutdown

`WaitForJobsToComplete = true` ensures running jobs finish before the process exits.

---

## Acceptance Criteria

- [ ] `OutboundSyncJob` triggers every minute by default.
- [ ] `InboundSyncJob` triggers every 5 minutes by default.
- [ ] A second trigger fires while the job is running → skipped. Logged as WARN.
- [ ] Application shutdown waits for in-progress job to complete before exiting.
- [ ] Cron expressions are configurable via `appsettings` without recompiling.
- [ ] EF Core `DbContext` is resolved from a per-execution `IServiceScope`, not injected as constructor parameter.
