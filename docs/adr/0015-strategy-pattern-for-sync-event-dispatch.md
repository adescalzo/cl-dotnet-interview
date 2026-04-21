# 0015 - Strategy pattern for sync event dispatch

- Status: accepted
- Date: 2026-04-21
- Deciders: TodoApi team

## Context and problem statement

The `OutboundSyncJob` must decide what external API call to make for each
`SyncEvent`. The decision depends on two dimensions: `EntityType`
(`TodoList` / `TodoItem`) and `EventType` (`Created` / `Updated` /
`Deleted`). That is six combinations today, and the matrix grows if new
entity types or event types are added.

A single method with nested `if`/`switch` blocks handles all six cases
in one place. This works initially but breaks down as the module grows:
adding a new event type requires editing the same class — violating the
Open/Closed Principle — and the class accumulates responsibilities that
belong to distinct use cases.

## Decision drivers

- Each combination of `(EntityType, EventType)` has different logic,
  different external API calls, and different mapping side-effects.
  They should be independently changeable and testable.
- Adding a new entity type or event type should not require modifying
  existing processor code — only adding a new strategy.
- Each strategy must be discoverable and injectable via DI — no manual
  `new` calls inside the job.
- Aligns with the SOLID principles already in use in the project
  (ADR-0002, ADR-0003).

## Considered options

- **Single processor class with switch/if** — all logic in one place;
  easy to find; violates OCP and SRP as the case count grows.
- **Strategy pattern** — one interface, one implementation per case;
  OCP-compliant; each strategy is independently testable.
- **Chain of Responsibility** — handlers chained; each decides whether
  to handle or pass on. More flexible than Strategy but adds indirection
  for a deterministic dispatch (the key is known at dispatch time).

## Decision outcome

Chosen option: **Strategy pattern**.

---

### SOLID alignment

The Strategy pattern is chosen because it is the most direct expression
of the SOLID principles that govern this codebase.

**Single Responsibility (SRP)**
Each strategy class has exactly one job: handle one `(EntityType, EventType)`
combination. `TodoListCreatedStrategy` knows only how to push a new list.
`TodoItemDeletedStrategy` knows only how to delete an item. Neither knows
about the other.

**Open/Closed (OCP)**
Adding a new entity type (`TodoLabel`) or a new event type (`Archived`)
requires writing a new strategy class and registering it. The job and the
dispatcher do not change.

**Liskov Substitution (LSP)**
All strategies implement `ISyncEventStrategy`. The dispatcher holds a
collection of `ISyncEventStrategy` and calls `ExecuteAsync` on the
matched one — any strategy can be substituted without the dispatcher
knowing which concrete type it is working with.

**Interface Segregation (ISP)**
`ISyncEventStrategy` is narrow: `CanHandle(SyncEvent)` and
`ExecuteAsync(SyncEvent, CancellationToken)`. Strategies do not
implement unrelated methods. Each strategy gets exactly what it needs
via constructor injection.

**Dependency Inversion (DIP)**
The job depends on `IEnumerable<ISyncEventStrategy>` — an abstraction.
Concrete strategies depend on `IExternalTodoApiClient`, `ISyncMappingRepository`,
etc. — also abstractions. No concrete type is referenced at the dispatch
level.

---

### Conventions

#### 1. Interface

```csharp
// Application/Jobs/Strategies/ISyncEventStrategy.cs
public interface ISyncEventStrategy
{
    bool CanHandle(SyncEvent syncEvent);
    Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct);
}
```

`CanHandle` is used by the dispatcher to route the event to the correct
strategy. It must be deterministic and side-effect-free.

#### 2. Dispatcher

```csharp
// Application/Jobs/SyncEventDispatcher.cs
public sealed class SyncEventDispatcher
{
    private readonly IEnumerable<ISyncEventStrategy> _strategies;

    public SyncEventDispatcher(IEnumerable<ISyncEventStrategy> strategies)
        => _strategies = strategies;

    public async Task DispatchAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        var strategy = _strategies.FirstOrDefault(s => s.CanHandle(syncEvent))
            ?? throw new InvalidOperationException(
                $"No strategy registered for {syncEvent.EntityType}/{syncEvent.EventType}");

        await strategy.ExecuteAsync(syncEvent, ct);
    }
}
```

The dispatcher does not know about any concrete strategy. It is a pure
routing mechanism.

#### 3. Strategy implementations

One class per `(EntityType, EventType)` combination:

```
Application/Jobs/Strategies/
  TodoListCreatedStrategy.cs    → POST /todolists
  TodoListUpdatedStrategy.cs    → PATCH /todolists/{id}
  TodoListDeletedStrategy.cs    → DELETE /todolists/{id}
  TodoItemCreatedStrategy.cs    → DELETE + POST (delete-and-recreate)
  TodoItemUpdatedStrategy.cs    → PATCH /todolists/{listId}/todoitems/{itemId}
  TodoItemDeletedStrategy.cs    → DELETE /todolists/{listId}/todoitems/{itemId}
```

Example:

```csharp
public sealed class TodoListDeletedStrategy : ISyncEventStrategy
{
    private readonly IExternalTodoApiClient _client;
    private readonly ISyncMappingRepository _mappings;

    public TodoListDeletedStrategy(
        IExternalTodoApiClient client,
        ISyncMappingRepository mappings)
    {
        _client = client;
        _mappings = mappings;
    }

    public bool CanHandle(SyncEvent e) =>
        e.EntityType == EntityType.TodoList && e.EventType == EventType.Deleted;

    public async Task ExecuteAsync(SyncEvent e, CancellationToken ct)
    {
        var mapping = await _mappings.FindByLocalIdAsync(EntityType.TodoList, e.EntityId, ct);
        if (mapping is null) return;   // already gone — idempotent

        try { await _client.DeleteTodoListAsync(mapping.ExternalId, ct); }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }

        await _mappings.DeleteByLocalIdAsync(EntityType.TodoList, e.EntityId, ct);
    }
}
```

#### 4. Registration

In `Infrastructure/Configuration/`:

```csharp
services.AddScoped<ISyncEventStrategy, TodoListCreatedStrategy>();
services.AddScoped<ISyncEventStrategy, TodoListUpdatedStrategy>();
services.AddScoped<ISyncEventStrategy, TodoListDeletedStrategy>();
services.AddScoped<ISyncEventStrategy, TodoItemCreatedStrategy>();
services.AddScoped<ISyncEventStrategy, TodoItemUpdatedStrategy>();
services.AddScoped<ISyncEventStrategy, TodoItemDeletedStrategy>();
services.AddScoped<SyncEventDispatcher>();
```

Scrutor (`AddFromAssembly`) can auto-register all `ISyncEventStrategy`
implementations from the assembly if the set grows:

```csharp
services.Scan(scan => scan
    .FromAssemblyOf<ISyncEventStrategy>()
    .AddClasses(c => c.AssignableTo<ISyncEventStrategy>())
    .AsImplementedInterfaces()
    .WithScopedLifetime());
```

#### 5. Usage in OutboundSyncJob

The job creates a DI scope per execution (ADR-0012), resolves the
dispatcher, and calls it for each coalesced event:

```csharp
public async Task Execute(IJobExecutionContext context)
{
    await using var scope = _scopeFactory.CreateAsyncScope();
    var dispatcher = scope.ServiceProvider.GetRequiredService<SyncEventDispatcher>();
    var repo = scope.ServiceProvider.GetRequiredService<ISyncEventRepository>();

    var events = await repo.GetPendingAsync(batchSize: 50, context.CancellationToken);

    foreach (var evt in Coalesce(events))
    {
        try
        {
            await dispatcher.DispatchAsync(evt, context.CancellationToken);
            await repo.MarkCompletedAsync(evt.Id, context.CancellationToken);
        }
        catch (Exception ex)
        {
            await repo.MarkFailedAsync(evt.Id, ex.Message, context.CancellationToken);
            _logger.LogError(ex, "Failed {EntityType}/{EventType} {EntityId}",
                evt.EntityType, evt.EventType, evt.EntityId);
        }
    }
}
```

The job has no knowledge of any specific strategy — only the dispatcher
and the event repository. Business logic stays in strategies, job stays
as orchestration.

### Consequences

- Positive: each strategy is a small, focused class with a single
  reason to change. Unit-testable in isolation with NSubstitute mocks.
- Positive: adding a new entity type requires only a new strategy class
  and a DI registration — zero changes to existing code.
- Positive: the dispatcher loop in the job shrinks to a single
  `DispatchAsync` call per event — readable and reviewable.
- Negative: six classes instead of one. The indirection is worth it at
  this complexity level; it would be overkill for two cases.
- Negative: `CanHandle` must be kept consistent with actual behavior.
  An incorrect `CanHandle` silently routes to the wrong strategy or
  throws at runtime. Covered by unit tests.
- Neutral: Scrutor auto-registration is convenient but makes the
  strategy set implicit. For six known strategies, explicit registration
  is preferred — it is visible in one place and survives renames.

## Links

- Applies to: ADR-0012 (Quartz job that uses the dispatcher).
- Applies to: ADR-0013 (Refit client injected into strategies).
- Applies to: ADR-0014 (Polly policies apply transparently — strategies
  do not know about retry logic).
- Related: ADR-0003 (DDD — same SOLID foundations across the codebase).
