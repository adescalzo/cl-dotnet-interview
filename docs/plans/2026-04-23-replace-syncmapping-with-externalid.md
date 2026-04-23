# Replace `SyncMapping` Table with `ExternalId` Column Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the `SyncMapping` entity / table / repository. Outbound strategies become payload-in / HTTP-out — they deserialize the `SyncEvent.Payload`, call the external API, and return; `OutboundSyncJob` marks the `SyncEvent` completed. The external system's id is stored (inbound-only) as a nullable `ExternalId` column on `TodoList` and `TodoItem`, written by `InboundSyncJob` when it ingests external-originated lists.

**Architecture:**

- **Outbound strategies are pure payload-in / HTTP-out.** They do not read or write `TodoList` / `TodoItem`. They deserialize `SyncEvent.Payload`, call the external API, and return. `OutboundSyncJob` marks the `SyncEvent` `Completed` on success or `Failed` on exception (already wired — `OutboundSyncJob.cs:38` and :43). Idempotency is the event's `Status` — the job only picks up `Pending` events.
- **`ExternalId` on aggregates is inbound-only.** `InboundSyncJob` writes it when it ingests external-originated lists/items, so subsequent `GET /todolists` polls don't re-insert the externally-injected ones (mock returns them with `SourceId = null`, so local-id matching can't dedupe them).
- **The external mock's `FindList` accepts either external `Id` or caller-supplied `SourceId`.** That lets outbound update/delete use `payload.Id.ToString()` as the route parameter without any translation table and without the aggregate.
- The old `SyncMapping` table encoded a 1:1 correlation in a junction table, plus drove outbound idempotency via mapping-exists checks. Both roles are eliminated: inbound correlation becomes a column on the aggregate; outbound idempotency is the `SyncEvent.Status`.

**Tech Stack:** .NET 9, EF Core SQL Server (InMemory for unit tests), Wolverine, Quartz, Refit, FluentAssertions, NSubstitute, Bogus, CSharpier.

---

## Scope

**In scope**
1. ADR-0016 recording the decision (supersedes the storage aspect of ADR-0015; strategy pattern itself is retained).
2. Add nullable `ExternalId` (string, max-len 500 to match the dropped `SyncMapping.ExternalId`) to `TodoList` and `TodoItem` aggregates, plus EF configurations.
3. Rewrite all six outbound sync strategies payload-in / HTTP-out (no aggregate access).
4. Rewrite `InboundSyncJob` to match external payload → local entity via: (a) `SourceId` → local `Id`, else (b) local row with matching `ExternalId`, else (c) insert a new local row with the external id captured in `ExternalId`. Inbound must both **insert** and **update** `TodoList`.
5. Delete: `SyncMapping` entity, `SyncMappingRepository`, `ISyncMappingRepository`, `SyncMappingCommandRepository` (test support), `SyncMappingConfiguration`, `TodoContext.SyncMapping` DbSet, DI registration in `PersistenceExtensions`.
6. Add `CorrelationId` (Guid, NOT NULL) to `SyncEvent`. Send it as the `X-Correlation-Id` HTTP header on every outbound Refit call. Purpose: idempotency on the external API if it honors the header. Document in `NOTES.md`.
7. Batch `OutboundSyncJob` saves — flush every N processed events instead of once per event. Constant `DefaultBatchSize = 10`. Document in `NOTES.md` that this assumes the external API is idempotent (the `CorrelationId` is how we buy that property from providers that honor it).
8. Extract the batch size into a `ProcessOptions` class using the Options pattern. Binding section `Process`. Registered in `ApplicationExtensions`. Injected into `OutboundSyncJob`.
9. Delete branch-local migrations `20260421130419_AddSyncModule` + `20260422022609_TodoItemGuidPk` and regenerate one clean migration that includes `CorrelationId`, `ExternalId`, and the existing columns.
10. Update existing tests: `OutboundSyncJobTests`, `TodoListCreatedStrategyTests`, `TodoListDeletedStrategyTests`.

**Out of scope (explicitly)**
- Adding tests for previously-untested strategies (`TodoListUpdated`, all three `TodoItem*`, `InboundSyncJob`). Those are existing gaps, not introduced by this refactor. Flag in commit body as follow-up work.
- Conflict resolution / optimistic-lock semantics when an externally-injected update races a local edit.
- `ExternalUpdatedAt` delta-cursor pulls.
- Paginated inbound fetch (user mentioned as a possible future improvement).
- Server-side idempotency in the external mock. The mock does not honor `X-Correlation-Id`; the header is defensive for when the real API does.

**Naming note.** The field is called `CorrelationId` per the user's request. Sent as HTTP header `X-Correlation-Id`. The semantic purpose is idempotency, which is closer to the `Idempotency-Key` header convention — but we match the local name rather than introduce a second identifier. `NOTES.md` calls this out.

---

## File Structure

### Created
- `docs/adr/0016-replace-syncmapping-table-with-externalid-column.md`
- `docs/plans/2026-04-23-replace-syncmapping-with-externalid.md` (this file)
- `TodoApi/Infrastructure/Settings/ProcessOptions.cs`

### Modified
- `CLAUDE.md` (ADR table)
- `NOTES.md` (CorrelationId + batching sections)
- `TodoApi/Data/Entities/TodoList.cs`
- `TodoApi/Data/Entities/TodoItem.cs`
- `TodoApi/Data/Entities/SyncEvent.cs` (add CorrelationId)
- `TodoApi/Data/Configuration/TodoListConfiguration.cs`
- `TodoApi/Data/Configuration/TodoItemConfiguration.cs`
- `TodoApi/Data/Configuration/SyncEventConfiguration.cs` (configure CorrelationId)
- `TodoApi/Data/TodoContext.cs`
- `TodoApi/Application/ExternalApi/IExternalTodoApiClient.cs` (X-Correlation-Id header param on mutation methods)
- `TodoApi/Application/Jobs/Strategies/TodoListCreatedStrategy.cs`
- `TodoApi/Application/Jobs/Strategies/TodoListUpdatedStrategy.cs`
- `TodoApi/Application/Jobs/Strategies/TodoListDeletedStrategy.cs`
- `TodoApi/Application/Jobs/Strategies/TodoItemCreatedStrategy.cs`
- `TodoApi/Application/Jobs/Strategies/TodoItemUpdatedStrategy.cs`
- `TodoApi/Application/Jobs/Strategies/TodoItemDeletedStrategy.cs`
- `TodoApi/Application/Jobs/OutboundSyncJob.cs` (batch save + ProcessOptions)
- `TodoApi/Application/Jobs/InboundSyncJob.cs`
- `TodoApi/Infrastructure/Configuration/PersistenceExtensions.cs`
- `TodoApi/Infrastructure/Configuration/ApplicationExtensions.cs` (bind ProcessOptions)
- `appsettings.json` + `appsettings.Development.json` (add `Process.BatchSize`)
- `TodoApi.Tests/Application/Jobs/OutboundSyncJobTests.cs`
- `TodoApi.Tests/Application/Jobs/Strategies/TodoListCreatedStrategyTests.cs`
- `TodoApi.Tests/Application/Jobs/Strategies/TodoListDeletedStrategyTests.cs`

### Deleted
- `TodoApi/Data/Entities/SyncMapping.cs`
- `TodoApi/Data/Configuration/SyncMappingConfiguration.cs`
- `TodoApi/Infrastructure/Persistence/SyncMappingRepository.cs`
- `TodoApi.Tests/TestSupport/SyncMappingCommandRepository.cs`
- `TodoApi/Migrations/20260421130419_AddSyncModule.cs` + `.Designer.cs`
- `TodoApi/Migrations/20260422022609_TodoItemGuidPk.cs` + `.Designer.cs`
- `TodoApi/Migrations/TodoContextModelSnapshot.cs` (regenerated)

### Regenerated
- `TodoApi/Migrations/<new-timestamp>_AddSyncModule.cs` + `.Designer.cs`
- `TodoApi/Migrations/TodoContextModelSnapshot.cs`

---

## Task 1: ADR-0016 + CLAUDE.md table entry

**Files:**
- Create: `docs/adr/0016-replace-syncmapping-table-with-externalid-column.md`
- Modify: `CLAUDE.md` (ADR table row)

- [ ] **Step 1: Write ADR-0016**

Create `docs/adr/0016-replace-syncmapping-table-with-externalid-column.md`:

```markdown
# 0016 - Replace SyncMapping table with ExternalId column on aggregates

- Status: accepted
- Date: 2026-04-23
- Deciders: adescalzo

## Context and problem statement

The sync module originally modeled local↔external id correlation with a
dedicated `SyncMapping` table (`EntityType`, `LocalId`, `ExternalId`,
`ExternalUpdatedAt`, `LastSyncedAt`). Every outbound strategy wrote a
parallel mapping row; `InboundSyncJob` looked up mappings by external id
to decide insert vs update.

Two observations collapsed this design:

1. The external API (`TodoStore.FindList`) resolves lookups by **either**
   the external `Id` **or** the caller-supplied `SourceId` (our local
   `Guid.ToString()`). For entities we own, outbound update/delete do not
   need a translation table — the local id works as the URL parameter.
2. The only hard requirement for a correlation store is the externally
   injected-case (inbound payloads with `SourceId == null`, which the
   mock produces on every `GET /todolists` via random injection). That
   correlation is 1:1 with the local aggregate, so a column on the
   aggregate models it without a junction table.

## Decision drivers

- Eliminate dual-write consistency problems (strategy updates the
  external API, then writes to `SyncMapping` — two writes, two failure
  modes).
- One aggregate, one row. Correlation metadata travels with the entity.
- Fewer moving parts: delete the entity, repository, interface, EF
  configuration, DbSet, DI registration, and test-support double.
- Drop the inverted/mapping-based guard at `TodoListCreatedStrategy.cs:28`
  entirely. Outbound idempotency is `SyncEvent.Status` (the
  `OutboundSyncJob` filter picks up `Pending` only); neither the
  aggregate nor a mapping needs to participate.

## Considered options

- **A.** Keep `SyncMapping` table.
- **B.** Nullable `ExternalId` string column on `TodoList` and `TodoItem`.
- **C.** Value-object `ExternalReference(ExternalId, ExternalUpdatedAt,
  LastSyncedAt)` owned by the aggregate.

## Decision outcome

Chosen option: **B**, because it models a 1:1 relationship without a
junction table, keeps correlation metadata with the aggregate, and
deletes the most code. `ExternalUpdatedAt` / `LastSyncedAt` fields from
the mapping are dropped: they were only read for delta-skip in
`InboundSyncJob`, and the current design does a full upsert on every
poll. When delta-cursoring is needed, a per-collection `SyncCursor`
table is the right shape, not per-entity columns.

Option C is the right shape if this grows additional correlation
metadata (e.g. provider, etag, retry counters). Until there is a second
field, it is one field dressed as a value object.

### Consequences

- Positive: strategies shrink to one I/O per use case; inbound job
  reads a single column; the branch's migration becomes a single
  `ExternalId` column and a `SyncEvent` table.
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
```

- [ ] **Step 2: Append row to the CLAUDE.md ADR table**

Open `CLAUDE.md`, find the existing table that lists ADRs (starts with
`| #    | Title`), and append:

```markdown
| 0016 | Replace SyncMapping table with ExternalId column on aggregates | Nullable `ExternalId` on TodoList/TodoItem; drop SyncMapping |
```

- [ ] **Step 3: Commit**

```bash
git add docs/adr/0016-replace-syncmapping-table-with-externalid-column.md CLAUDE.md docs/plans/2026-04-23-replace-syncmapping-with-externalid.md
git commit -m "docs(adr): ADR-0016 replace SyncMapping with ExternalId column

Records the decision to drop the SyncMapping table in favor of a
nullable ExternalId column on TodoList and TodoItem aggregates.
Supersedes the storage shape from ADR-0015 (strategy pattern retained)."
```

---

## Task 2: Add `ExternalId` to `TodoList` (+ test)

**Files:**
- Modify: `TodoApi/Data/Entities/TodoList.cs`
- Modify: `TodoApi.Tests/Data/TodoListTests.cs`

- [ ] **Step 1: Write failing test for `LinkExternal`**

Open `TodoApi.Tests/Data/TodoListTests.cs` and add:

```csharp
[Fact]
public void LinkExternal_WhenCalled_ShouldSetExternalId()
{
    var list = new TodoListBuilder().Build();

    list.LinkExternal("ext-list-1");

    list.ExternalId.Should().Be("ext-list-1");
}

[Fact]
public void LinkExternal_WhenAlreadyLinked_ShouldOverwrite()
{
    var list = new TodoListBuilder().Build();
    list.LinkExternal("ext-old");

    list.LinkExternal("ext-new");

    list.ExternalId.Should().Be("ext-new");
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test --filter "FullyQualifiedName~TodoListTests.LinkExternal" --nologo
```

Expected: FAIL with "TodoList does not contain a definition for LinkExternal / ExternalId".

- [ ] **Step 3: Add property and method to `TodoList`**

Edit `TodoApi/Data/Entities/TodoList.cs`:

1. Add property after `IsDeleted`:

```csharp
public string? ExternalId { get; private set; }
```

2. Add method after `MarkAsDeleted`:

```csharp
public void LinkExternal(string externalId)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
    ExternalId = externalId;
}
```

- [ ] **Step 4: Run test to verify pass**

```bash
dotnet test --filter "FullyQualifiedName~TodoListTests.LinkExternal" --nologo
```

Expected: PASS (both cases).

- [ ] **Step 5: Commit**

```bash
git add TodoApi/Data/Entities/TodoList.cs TodoApi.Tests/Data/TodoListTests.cs
git commit -m "feat(todolist): add ExternalId + LinkExternal"
```

---

## Task 3: Add `ExternalId` to `TodoItem` (+ test)

**Files:**
- Modify: `TodoApi/Data/Entities/TodoItem.cs`
- Modify: `TodoApi.Tests/Data/TodoItemTests.cs`

- [ ] **Step 1: Write failing test for `LinkExternal`**

Append to `TodoApi.Tests/Data/TodoItemTests.cs`:

```csharp
[Fact]
public void LinkExternal_WhenCalled_ShouldSetExternalId()
{
    var item = new TodoItemBuilder().Build();

    item.LinkExternal("ext-item-1");

    item.ExternalId.Should().Be("ext-item-1");
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test --filter "FullyQualifiedName~TodoItemTests.LinkExternal" --nologo
```

Expected: FAIL.

- [ ] **Step 3: Add property and method to `TodoItem`**

Edit `TodoApi/Data/Entities/TodoItem.cs`:

1. Add property after `IsDeleted`:

```csharp
public string? ExternalId { get; private set; }
```

2. Add method after `MarkAsDeleted`:

```csharp
public void LinkExternal(string externalId)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
    ExternalId = externalId;
}
```

- [ ] **Step 4: Run test to verify pass**

```bash
dotnet test --filter "FullyQualifiedName~TodoItemTests.LinkExternal" --nologo
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add TodoApi/Data/Entities/TodoItem.cs TodoApi.Tests/Data/TodoItemTests.cs
git commit -m "feat(todoitem): add ExternalId + LinkExternal"
```

---

## Task 4: EF Core configuration for `ExternalId` on both aggregates

**Files:**
- Modify: `TodoApi/Data/Configuration/TodoListConfiguration.cs`
- Modify: `TodoApi/Data/Configuration/TodoItemConfiguration.cs`

- [ ] **Step 1: Configure `TodoList.ExternalId`**

Edit `TodoApi/Data/Configuration/TodoListConfiguration.cs`; after the
`Name` / `CreatedAt` property lines inside `Configure`, add:

```csharp
builder.Property(t => t.ExternalId).HasMaxLength(500);
builder.HasIndex(t => t.ExternalId).IsUnique().HasFilter("[ExternalId] IS NOT NULL");
```

- [ ] **Step 2: Configure `TodoItem.ExternalId`**

Edit `TodoApi/Data/Configuration/TodoItemConfiguration.cs`; after the
existing property lines, add:

```csharp
builder.Property(t => t.ExternalId).HasMaxLength(500);
builder.HasIndex(t => t.ExternalId).IsUnique().HasFilter("[ExternalId] IS NOT NULL");
```

Rationale for the filtered unique index: `ExternalId` is nullable. A
plain unique index would reject multiple NULLs on SQL Server; the
filtered variant allows many nulls but forbids duplicate non-null
values. InMemory provider ignores the filter — harmless.

- [ ] **Step 3: Build to verify nothing broke**

```bash
dotnet build --nologo
```

Expected: build succeeds (no new warnings).

- [ ] **Step 4: Commit**

```bash
git add TodoApi/Data/Configuration/TodoListConfiguration.cs TodoApi/Data/Configuration/TodoItemConfiguration.cs
git commit -m "feat(persistence): configure ExternalId column on TodoList/TodoItem"
```

---

## Task 5: Rewrite `TodoListCreatedStrategy` (+ update test)

The strategy deserializes the payload and calls the external API. It
does not read or write the `TodoList` aggregate. `OutboundSyncJob`
marks the `SyncEvent` completed on return.

**Files:**
- Modify: `TodoApi/Application/Jobs/Strategies/TodoListCreatedStrategy.cs`
- Modify: `TodoApi.Tests/Application/Jobs/Strategies/TodoListCreatedStrategyTests.cs`

- [ ] **Step 1: Rewrite the strategy**

Replace the body of `TodoApi/Application/Jobs/Strategies/TodoListCreatedStrategy.cs`:

```csharp
using System.Text.Json;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Payloads;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoListCreatedStrategy(
    IExternalTodoApiClient client,
    ILogger<TodoListCreatedStrategy> logger
) : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoList, EventType: EventType.Created };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        try
        {
            var payload = JsonSerializer.Deserialize<TodoListCreatedPayload>(syncEvent.Payload)!;

            await client
                .CreateTodoListAsync(
                    new CreateExternalTodoListRequest(payload.Id.ToString(), payload.Name, []),
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogTodoListCreatedStrategyFailed(syncEvent.Id, syncEvent.EntityId, ex);
            throw;
        }
    }
}

internal static partial class TodoListCreatedStrategyLoggerDefinition
{
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Error,
        EventName = "TodoListCreatedStrategyFailed",
        Message = "TodoListCreated strategy failed for SyncEventId: {SyncEventId}, EntityId: {EntityId}"
    )]
    public static partial void LogTodoListCreatedStrategyFailed(
        this ILogger logger,
        Guid syncEventId,
        Guid entityId,
        Exception ex
    );
}
```

- [ ] **Step 2: Rewrite the test**

Replace the body of `TodoApi.Tests/Application/Jobs/Strategies/TodoListCreatedStrategyTests.cs` with a pure unit test (no EF, no `AsyncLifetimeBase`). The strategy only makes an HTTP call now.

```csharp
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Payloads;
using TodoApi.Application.Jobs.Strategies;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure.Extensions;

namespace TodoApi.Tests.Application.Jobs.Strategies;

public sealed class TodoListCreatedStrategyTests
{
    private readonly IExternalTodoApiClient _client = Substitute.For<IExternalTodoApiClient>();

    [Fact]
    public async Task ExecuteAsync_WhenCalled_ShouldPostPayloadToExternalApi()
    {
        var id = GuidV7.NewGuid();
        var payload = new TodoListCreatedPayload(id, "groceries");
        var syncEvent = new SyncEvent(
            EntityType.TodoList, id, EventType.Created, JsonSerializer.Serialize(payload));

        _client.CreateTodoListAsync(Arg.Any<CreateExternalTodoListRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalTodoList("ext-1", id.ToString(), "groceries", DateTime.UtcNow, DateTime.UtcNow, []));

        var sut = new TodoListCreatedStrategy(_client, NullLogger<TodoListCreatedStrategy>.Instance);

        await sut.ExecuteAsync(syncEvent, CancellationToken.None);

        await _client.Received(1).CreateTodoListAsync(
            Arg.Is<CreateExternalTodoListRequest>(r =>
                r.SourceId == id.ToString()
                && r.Name == "groceries"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenExternalApiThrows_ShouldPropagate()
    {
        var id = GuidV7.NewGuid();
        var payload = new TodoListCreatedPayload(id, "x");
        var syncEvent = new SyncEvent(
            EntityType.TodoList, id, EventType.Created, JsonSerializer.Serialize(payload));

        _client.CreateTodoListAsync(Arg.Any<CreateExternalTodoListRequest>(), Arg.Any<CancellationToken>())
            .Returns<ExternalTodoList>(_ => throw new HttpRequestException("boom"));

        var sut = new TodoListCreatedStrategy(_client, NullLogger<TodoListCreatedStrategy>.Instance);

        var act = async () => await sut.ExecuteAsync(syncEvent, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
```

Note: verify the `ExternalTodoList` constructor signature on the Refit
payload type before running — the mock's DTO and the Refit DTO may differ
in property order. Adjust positional args if needed.

- [ ] **Step 3: Run tests**

```bash
dotnet test --filter "FullyQualifiedName~TodoListCreatedStrategyTests" --nologo
```

Expected: PASS (2 tests).

- [ ] **Step 4: Commit**

```bash
git add TodoApi/Application/Jobs/Strategies/TodoListCreatedStrategy.cs TodoApi.Tests/Application/Jobs/Strategies/TodoListCreatedStrategyTests.cs
git commit -m "refactor(sync): TodoListCreatedStrategy is payload-in HTTP-out

Strategy no longer reads or writes aggregates. OutboundSyncJob
marks the SyncEvent completed on return. Drops the inverted
mapping guard entirely."
```

---

## Task 6: Rewrite `TodoListUpdatedStrategy`

No existing test. Not adding one (out of scope — flagged in follow-up).
Payload carries everything needed; strategy does not touch aggregates.

**Files:**
- Modify: `TodoApi/Application/Jobs/Strategies/TodoListUpdatedStrategy.cs`

- [ ] **Step 1: Rewrite the strategy**

Replace the file with:

```csharp
using System.Text.Json;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Payloads;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoListUpdatedStrategy(
    IExternalTodoApiClient client,
    ILogger<TodoListUpdatedStrategy> logger
) : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoList, EventType: EventType.Updated };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        try
        {
            var payload = JsonSerializer.Deserialize<TodoListUpdatedPayload>(syncEvent.Payload)!;

            await client
                .UpdateTodoListAsync(
                    payload.Id.ToString(),
                    new UpdateExternalTodoListRequest(payload.Name),
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogTodoListUpdatedStrategyFailed(syncEvent.Id, syncEvent.EntityId, ex);
            throw;
        }
    }
}

internal static partial class TodoListUpdatedStrategyLoggerDefinition
{
    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Error,
        EventName = "TodoListUpdatedStrategyFailed",
        Message = "TodoListUpdated strategy failed for SyncEventId: {SyncEventId}, EntityId: {EntityId}"
    )]
    public static partial void LogTodoListUpdatedStrategyFailed(
        this ILogger logger,
        Guid syncEventId,
        Guid entityId,
        Exception ex
    );
}
```

The route parameter is `payload.Id.ToString()` — the mock's `FindList`
resolves that as `SourceId`. No mapping read, no aggregate read.

- [ ] **Step 2: Build**

```bash
dotnet build --nologo
```

- [ ] **Step 3: Commit**

```bash
git add TodoApi/Application/Jobs/Strategies/TodoListUpdatedStrategy.cs
git commit -m "refactor(sync): TodoListUpdatedStrategy is payload-in HTTP-out"
```

---

## Task 7: Rewrite `TodoListDeletedStrategy` (+ update test)

**Files:**
- Modify: `TodoApi/Application/Jobs/Strategies/TodoListDeletedStrategy.cs`
- Modify: `TodoApi.Tests/Application/Jobs/Strategies/TodoListDeletedStrategyTests.cs`

- [ ] **Step 1: Rewrite the strategy**

Replace the file with:

```csharp
using System.Net;
using System.Text.Json;
using Refit;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoListDeletedStrategy(
    IExternalTodoApiClient client,
    ILogger<TodoListDeletedStrategy> logger
) : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoList, EventType: EventType.Deleted };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        try
        {
            var payload = JsonSerializer.Deserialize<TodoListDeletedPayload>(syncEvent.Payload)!;

            try
            {
                await client.DeleteTodoListAsync(payload.Id.ToString(), ct).ConfigureAwait(false);
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _ = ex;
            }
        }
        catch (Exception ex)
        {
            logger.LogTodoListDeletedStrategyFailed(syncEvent.Id, syncEvent.EntityId, ex);
            throw;
        }
    }
}

internal static partial class TodoListDeletedStrategyLoggerDefinition
{
    [LoggerMessage(
        EventId = 1300,
        Level = LogLevel.Error,
        EventName = "TodoListDeletedStrategyFailed",
        Message = "TodoListDeleted strategy failed for SyncEventId: {SyncEventId}, EntityId: {EntityId}"
    )]
    public static partial void LogTodoListDeletedStrategyFailed(
        this ILogger logger,
        Guid syncEventId,
        Guid entityId,
        Exception ex
    );
}
```

404 is swallowed: the external resource is already gone, the local
delete event still completes cleanly.

- [ ] **Step 2: Rewrite the test**

Replace the body of `TodoApi.Tests/Application/Jobs/Strategies/TodoListDeletedStrategyTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Refit;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.Jobs.Strategies;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure.Extensions;

namespace TodoApi.Tests.Application.Jobs.Strategies;

public sealed class TodoListDeletedStrategyTests
{
    private readonly IExternalTodoApiClient _client = Substitute.For<IExternalTodoApiClient>();

    [Fact]
    public async Task ExecuteAsync_WhenCalled_ShouldCallDeleteWithPayloadId()
    {
        var id = GuidV7.NewGuid();
        var syncEvent = new SyncEvent(
            EntityType.TodoList, id, EventType.Deleted,
            JsonSerializer.Serialize(new TodoListDeletedPayload(id)));

        var sut = new TodoListDeletedStrategy(_client, NullLogger<TodoListDeletedStrategy>.Instance);

        await sut.ExecuteAsync(syncEvent, CancellationToken.None);

        await _client.Received(1).DeleteTodoListAsync(id.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenExternalReturnsNotFound_ShouldSwallow()
    {
        var id = GuidV7.NewGuid();
        var syncEvent = new SyncEvent(
            EntityType.TodoList, id, EventType.Deleted,
            JsonSerializer.Serialize(new TodoListDeletedPayload(id)));

        _client.DeleteTodoListAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw await ApiException.Create(
                new HttpRequestMessage(),
                HttpMethod.Delete,
                new HttpResponseMessage(HttpStatusCode.NotFound),
                new RefitSettings()));

        var sut = new TodoListDeletedStrategy(_client, NullLogger<TodoListDeletedStrategy>.Instance);

        var act = async () => await sut.ExecuteAsync(syncEvent, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
```

Note: `ApiException.Create` is a static factory. If the signature in
this Refit version differs, construct via the public constructor
instead — either works, keep the test compiling.

- [ ] **Step 3: Run tests**

```bash
dotnet test --filter "FullyQualifiedName~TodoListDeletedStrategyTests" --nologo
```

Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add TodoApi/Application/Jobs/Strategies/TodoListDeletedStrategy.cs TodoApi.Tests/Application/Jobs/Strategies/TodoListDeletedStrategyTests.cs
git commit -m "refactor(sync): TodoListDeletedStrategy is payload-in HTTP-out"
```

---

## Task 8: Rewrite `TodoItemCreatedStrategy`

**Files:**
- Modify: `TodoApi/Application/Jobs/Strategies/TodoItemCreatedStrategy.cs`

- [ ] **Step 1: Rewrite the strategy**

Replace the file with:

```csharp
using System.Text.Json;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Payloads;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoItemCreatedStrategy(
    IExternalTodoApiClient client,
    ILogger<TodoItemCreatedStrategy> logger
) : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoItem, EventType: EventType.Created };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        try
        {
            var payload = JsonSerializer.Deserialize<TodoItemCreatedPayload>(syncEvent.Payload)!;

            var request = new UpdateExternalTodoListRequest(
                Name: payload.TodoListName,
                Items:
                [
                    new CreateExternalTodoItemRequest(
                        payload.Id.ToString(),
                        payload.Name,
                        payload.IsComplete
                    ),
                ]
            );

            await client
                .UpdateTodoListAsync(payload.TodoListId.ToString(), request, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogTodoItemCreatedStrategyFailed(syncEvent.Id, syncEvent.EntityId, ex);
            throw;
        }
    }
}

internal static partial class TodoItemCreatedStrategyLoggerDefinition
{
    [LoggerMessage(
        EventId = 1400,
        Level = LogLevel.Error,
        EventName = "TodoItemCreatedStrategyFailed",
        Message = "TodoItemCreated strategy failed for SyncEventId: {SyncEventId}, EntityId: {EntityId}"
    )]
    public static partial void LogTodoItemCreatedStrategyFailed(
        this ILogger logger,
        Guid syncEventId,
        Guid entityId,
        Exception ex
    );
}
```

Payload-only. `TodoListId` and `TodoListName` come from the payload —
no aggregate lookup.

- [ ] **Step 2: Build**

```bash
dotnet build --nologo
```

- [ ] **Step 3: Commit**

```bash
git add TodoApi/Application/Jobs/Strategies/TodoItemCreatedStrategy.cs
git commit -m "refactor(sync): TodoItemCreatedStrategy is payload-in HTTP-out"
```

---

## Task 9: Rewrite `TodoItemUpdatedStrategy`

**Files:**
- Modify: `TodoApi/Application/Jobs/Strategies/TodoItemUpdatedStrategy.cs`

- [ ] **Step 1: Rewrite the strategy**

Replace the file with:

```csharp
using System.Text.Json;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Payloads;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoItemUpdatedStrategy(
    IExternalTodoApiClient client,
    ILogger<TodoItemUpdatedStrategy> logger
) : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoItem, EventType: EventType.Updated };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        try
        {
            var payload = JsonSerializer.Deserialize<TodoItemUpdatedPayload>(syncEvent.Payload)!;

            await client
                .UpdateTodoItemAsync(
                    payload.TodoListId.ToString(),
                    payload.Id.ToString(),
                    new UpdateExternalTodoItemRequest(payload.Name, payload.IsComplete),
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogTodoItemUpdatedStrategyFailed(syncEvent.Id, syncEvent.EntityId, ex);
            throw;
        }
    }
}

internal static partial class TodoItemUpdatedStrategyLoggerDefinition
{
    [LoggerMessage(
        EventId = 1500,
        Level = LogLevel.Error,
        EventName = "TodoItemUpdatedStrategyFailed",
        Message = "TodoItemUpdated strategy failed for SyncEventId: {SyncEventId}, EntityId: {EntityId}"
    )]
    public static partial void LogTodoItemUpdatedStrategyFailed(
        this ILogger logger,
        Guid syncEventId,
        Guid entityId,
        Exception ex
    );
}
```

- [ ] **Step 2: Build**

```bash
dotnet build --nologo
```

- [ ] **Step 3: Commit**

```bash
git add TodoApi/Application/Jobs/Strategies/TodoItemUpdatedStrategy.cs
git commit -m "refactor(sync): TodoItemUpdatedStrategy is payload-in HTTP-out"
```

---

## Task 10: Rewrite `TodoItemDeletedStrategy`

**Files:**
- Modify: `TodoApi/Application/Jobs/Strategies/TodoItemDeletedStrategy.cs`

- [ ] **Step 1: Rewrite the strategy**

Replace the file with:

```csharp
using System.Net;
using System.Text.Json;
using Refit;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.Sync;
using TodoApi.Data.Entities;

namespace TodoApi.Application.Jobs.Strategies;

public sealed class TodoItemDeletedStrategy(
    IExternalTodoApiClient client,
    ILogger<TodoItemDeletedStrategy> logger
) : ISyncEventStrategy
{
    public bool CanHandle(SyncEvent syncEvent) =>
        syncEvent is { EntityType: EntityType.TodoItem, EventType: EventType.Deleted };

    public async Task ExecuteAsync(SyncEvent syncEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);

        try
        {
            var payload = JsonSerializer.Deserialize<TodoItemDeletedPayload>(syncEvent.Payload)!;

            try
            {
                await client
                    .DeleteTodoItemAsync(
                        payload.TodoListId.ToString(),
                        payload.Id.ToString(),
                        ct
                    )
                    .ConfigureAwait(false);
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _ = ex;
            }
        }
        catch (Exception ex)
        {
            logger.LogTodoItemDeletedStrategyFailed(syncEvent.Id, syncEvent.EntityId, ex);
            throw;
        }
    }
}

internal static partial class TodoItemDeletedStrategyLoggerDefinition
{
    [LoggerMessage(
        EventId = 1600,
        Level = LogLevel.Error,
        EventName = "TodoItemDeletedStrategyFailed",
        Message = "TodoItemDeleted strategy failed for SyncEventId: {SyncEventId}, EntityId: {EntityId}"
    )]
    public static partial void LogTodoItemDeletedStrategyFailed(
        this ILogger logger,
        Guid syncEventId,
        Guid entityId,
        Exception ex
    );
}
```

- [ ] **Step 2: Build**

```bash
dotnet build --nologo
```

- [ ] **Step 3: Commit**

```bash
git add TodoApi/Application/Jobs/Strategies/TodoItemDeletedStrategy.cs
git commit -m "refactor(sync): TodoItemDeletedStrategy is payload-in HTTP-out"
```

---

## Task 11: Rewrite `InboundSyncJob`

**Files:**
- Modify: `TodoApi/Application/Jobs/InboundSyncJob.cs`

Semantics required by the user: inbound must **insert and update**
`TodoList`. Correlation resolution order per external list:
1. If `externalList.SourceId` is a parseable Guid and a local row with
   that `Id` exists — that's the correlation.
2. Else if a local row with `ExternalId == externalList.Id` exists —
   that's the correlation.
3. Else — insert a new local row, `LinkExternal(externalList.Id)`.

Items use the same resolution against the list's `Items` collection.

- [ ] **Step 1: Rewrite the job**

Replace the file body with:

```csharp
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Quartz;
using TodoApi.Application.ExternalApi;
using TodoApi.Application.ExternalApi.Payloads;
using TodoApi.Data;
using TodoApi.Data.Entities;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Hubs;
using TodoApi.Infrastructure.Persistence;

namespace TodoApi.Application.Jobs;

[DisallowConcurrentExecution]
public sealed class InboundSyncJob(
    IServiceScopeFactory scopeFactory,
    IHubContext<NotificationHub> hub,
    ILogger<InboundSyncJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await using var scope = scopeFactory.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<IExternalTodoApiClient>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var dbContext = scope.ServiceProvider.GetRequiredService<TodoContext>();

        var externalLists = await client.GetAllAsync(context.CancellationToken).ConfigureAwait(false);
        var synced = 0;

        foreach (var externalList in externalLists)
        {
            try
            {
                synced += await SyncListAsync(externalList, dbContext, clock, context.CancellationToken).ConfigureAwait(false);
                await uow.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogInboundSyncFailed(ex, externalList.Id);
            }
        }

        if (synced == 0)
        {
            return;
        }

        await hub.Clients.All
            .SendAsync("InboundSyncJob", new { Synced = synced }, context.CancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<int> SyncListAsync(
        ExternalTodoList externalList,
        TodoContext dbContext,
        IClock clock,
        CancellationToken ct
    )
    {
        var localList = await ResolveLocalListAsync(externalList, dbContext, ct).ConfigureAwait(false);
        var synced = 0;
        var now = clock.UtcNow;

        if (localList is null)
        {
            localList = new TodoList(externalList.Name, now);
            localList.LinkExternal(externalList.Id);
            await dbContext.TodoList.AddAsync(localList, ct).ConfigureAwait(false);
            synced++;
        }
        else
        {
            if (localList.ExternalId is null)
            {
                localList.LinkExternal(externalList.Id);
            }

            if (!string.Equals(localList.Name, externalList.Name, StringComparison.Ordinal))
            {
                localList.Update(externalList.Name, now);
                synced++;
            }
        }

        var order = localList.Items.Count;
        foreach (var externalItem in externalList.Items)
        {
            if (IsAlreadyLinked(localList, externalItem))
            {
                continue;
            }

            order++;
            var item = localList.AddItem(externalItem.Description, order, now);
            if (externalItem.Completed)
            {
                item.Complete(now);
            }
            item.LinkExternal(externalItem.Id);
            synced++;
        }

        return synced;
    }

    private static async Task<TodoList?> ResolveLocalListAsync(
        ExternalTodoList externalList,
        TodoContext dbContext,
        CancellationToken ct
    )
    {
        if (Guid.TryParse(externalList.SourceId, out var sourceLocalId))
        {
            var bySource = await dbContext.TodoList.Include(l => l.Items)
                .FirstOrDefaultAsync(l => l.Id == sourceLocalId, ct)
                .ConfigureAwait(false);
            if (bySource is not null)
            {
                return bySource;
            }
        }

        return await dbContext.TodoList.Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.ExternalId == externalList.Id, ct)
            .ConfigureAwait(false);
    }

    private static bool IsAlreadyLinked(TodoList list, ExternalTodoItem externalItem)
    {
        if (list.Items.Any(i => i.ExternalId == externalItem.Id))
        {
            return true;
        }

        if (Guid.TryParse(externalItem.SourceId, out var localId)
            && list.Items.Any(i => i.Id == localId))
        {
            return true;
        }

        return false;
    }
}

internal static partial class InboundSyncJobLoggerDefinition
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        EventName = "InboundSyncFailed",
        Message = "Inbound sync failed for external list {ExternalId}"
    )]
    public static partial void LogInboundSyncFailed(
        this ILogger logger,
        Exception ex,
        string externalId
    );
}
```

Name-diff check before `Update` prevents writing through every poll
for unchanged lists. Items are currently insert-only in inbound — the
previous version also did not update existing items; keeping that
scope.

- [ ] **Step 2: Build**

```bash
dotnet build --nologo
```

- [ ] **Step 3: Commit**

```bash
git add TodoApi/Application/Jobs/InboundSyncJob.cs
git commit -m "refactor(sync): rewrite InboundSyncJob against ExternalId

Resolution order: SourceId -> local Id, else ExternalId match, else
insert-with-link. List name changes update the local row; item
correlation is insert-only (unchanged scope)."
```

---

## Task 12: Delete `SyncMapping` machinery

At this point no code depends on `ISyncMappingRepository` or
`SyncMapping` anymore. Time to delete.

**Files:**
- Delete: `TodoApi/Data/Entities/SyncMapping.cs`
- Delete: `TodoApi/Data/Configuration/SyncMappingConfiguration.cs`
- Delete: `TodoApi/Infrastructure/Persistence/SyncMappingRepository.cs`
- Delete: `TodoApi.Tests/TestSupport/SyncMappingCommandRepository.cs`
- Modify: `TodoApi/Data/TodoContext.cs` (drop `DbSet<SyncMapping>`)
- Modify: `TodoApi/Infrastructure/Configuration/PersistenceExtensions.cs` (drop DI registration)
- Modify: `TodoApi.Tests/Application/Jobs/OutboundSyncJobTests.cs` (drop any `SyncMapping` fixture still present)

- [ ] **Step 1: Delete files**

```bash
git rm TodoApi/Data/Entities/SyncMapping.cs \
       TodoApi/Data/Configuration/SyncMappingConfiguration.cs \
       TodoApi/Infrastructure/Persistence/SyncMappingRepository.cs \
       TodoApi.Tests/TestSupport/SyncMappingCommandRepository.cs
```

- [ ] **Step 2: Drop `SyncMapping` DbSet from `TodoContext`**

Edit `TodoApi/Data/TodoContext.cs`: remove the line

```csharp
public DbSet<SyncMapping> SyncMapping { get; set; }
```

- [ ] **Step 3: Drop DI registration from `PersistenceExtensions`**

Edit `TodoApi/Infrastructure/Configuration/PersistenceExtensions.cs`: remove

```csharp
services.AddScoped<ISyncMappingRepository, SyncMappingRepository>();
```

- [ ] **Step 4: Drop `SyncMapping` fixtures from `OutboundSyncJobTests`**

Open `TodoApi.Tests/Application/Jobs/OutboundSyncJobTests.cs`. Remove:
- any `using` for SyncMapping
- any `ISyncMappingRepository` substitute or `SyncMappingCommandRepository` usage
- any assertion about mapping rows (they should now be entity-level assertions, not table assertions; if the existing test isn't already covered by the new strategy tests, keep the assertion at "strategy ran and ExternalId was set" rather than re-asserting on the mapping table).

- [ ] **Step 5: Build**

```bash
dotnet build --nologo
```

Expected: build passes. Any compile error here is a missed reference —
grep for `SyncMapping` and `ISyncMappingRepository` to find stragglers.

- [ ] **Step 6: Run full test suite**

```bash
dotnet test --nologo
```

Expected: all green. InMemory tests do not require a migration.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor(sync): remove SyncMapping entity, repository, and DI wiring

Unused after the strategy/inbound rewrites. Correlation now lives on
TodoList.ExternalId / TodoItem.ExternalId."
```

---

## Task 13: Add `CorrelationId` to `SyncEvent` + propagate through Refit client + all strategies + NOTES.md

**Files:**
- Modify: `TodoApi/Data/Entities/SyncEvent.cs`
- Modify: `TodoApi/Data/Configuration/SyncEventConfiguration.cs`
- Modify: `TodoApi/Application/ExternalApi/IExternalTodoApiClient.cs`
- Modify: all six strategy files under `TodoApi/Application/Jobs/Strategies/`
- Modify: `TodoApi.Tests/Application/Jobs/Strategies/TodoListCreatedStrategyTests.cs`
- Modify: `TodoApi.Tests/Application/Jobs/Strategies/TodoListDeletedStrategyTests.cs`
- Modify: `NOTES.md`

- [ ] **Step 1: Add `CorrelationId` to `SyncEvent`**

Edit `TodoApi/Data/Entities/SyncEvent.cs`. Add the property after `Id`:

```csharp
public Guid CorrelationId { get; private set; } = GuidV7.NewGuid();
```

No constructor change needed — the property initializer runs for both
the EF private ctor and the public ctor. This gives every new event a
fresh Guid v7. Events reconstituted from the DB overwrite the
initializer with the persisted value.

- [ ] **Step 2: Configure `CorrelationId` in EF**

Edit `TodoApi/Data/Configuration/SyncEventConfiguration.cs`. Inside
`Configure`, after the `CreatedAt` property line, add:

```csharp
builder.Property(s => s.CorrelationId).IsRequired();
```

Not `ValueGeneratedNever()` — EF will persist whatever value is on the
entity, which for new rows is the Guid v7 from the property initializer.

- [ ] **Step 3: Add `X-Correlation-Id` header param to Refit mutation methods**

Edit `TodoApi/Application/ExternalApi/IExternalTodoApiClient.cs`. Add a
`[Header("X-Correlation-Id")] string correlationId` parameter to the
five mutation methods. `GetAllAsync` is a read — leave it alone.

Replacement file:

```csharp
using Refit;
using TodoApi.Application.ExternalApi.Payloads;

namespace TodoApi.Application.ExternalApi;

public interface IExternalTodoApiClient
{
    [Get("/todolists")]
    Task<IReadOnlyList<ExternalTodoList>> GetAllAsync(CancellationToken ct = default);

    [Post("/todolists")]
    Task<ExternalTodoList> CreateTodoListAsync(
        [Header("X-Correlation-Id")] string correlationId,
        [Body] CreateExternalTodoListRequest body,
        CancellationToken ct = default
    );

    [Patch("/todolists/{todolistId}")]
    Task<ExternalTodoList> UpdateTodoListAsync(
        [Header("X-Correlation-Id")] string correlationId,
        string todolistId,
        [Body] UpdateExternalTodoListRequest body,
        CancellationToken ct = default
    );

    [Delete("/todolists/{todolistId}")]
    Task DeleteTodoListAsync(
        [Header("X-Correlation-Id")] string correlationId,
        string todolistId,
        CancellationToken ct = default
    );

    [Patch("/todolists/{todolistId}/todoitems/{todoitemId}")]
    Task<ExternalTodoItem> UpdateTodoItemAsync(
        [Header("X-Correlation-Id")] string correlationId,
        string todolistId,
        string todoitemId,
        [Body] UpdateExternalTodoItemRequest body,
        CancellationToken ct = default
    );

    [Delete("/todolists/{todolistId}/todoitems/{todoitemId}")]
    Task DeleteTodoItemAsync(
        [Header("X-Correlation-Id")] string correlationId,
        string todolistId,
        string todoitemId,
        CancellationToken ct = default
    );
}
```

- [ ] **Step 4: Thread `syncEvent.CorrelationId` through every strategy call**

For each of the six strategy files, prepend `syncEvent.CorrelationId.ToString()`
to the corresponding Refit call. Exact replacements:

`TodoListCreatedStrategy.cs` — inside the try:

```csharp
await client
    .CreateTodoListAsync(
        syncEvent.CorrelationId.ToString(),
        new CreateExternalTodoListRequest(payload.Id.ToString(), payload.Name, []),
        ct
    )
    .ConfigureAwait(false);
```

`TodoListUpdatedStrategy.cs`:

```csharp
await client
    .UpdateTodoListAsync(
        syncEvent.CorrelationId.ToString(),
        payload.Id.ToString(),
        new UpdateExternalTodoListRequest(payload.Name),
        ct
    )
    .ConfigureAwait(false);
```

`TodoListDeletedStrategy.cs`:

```csharp
await client
    .DeleteTodoListAsync(syncEvent.CorrelationId.ToString(), payload.Id.ToString(), ct)
    .ConfigureAwait(false);
```

`TodoItemCreatedStrategy.cs`:

```csharp
await client
    .UpdateTodoListAsync(
        syncEvent.CorrelationId.ToString(),
        payload.TodoListId.ToString(),
        request,
        ct
    )
    .ConfigureAwait(false);
```

`TodoItemUpdatedStrategy.cs`:

```csharp
await client
    .UpdateTodoItemAsync(
        syncEvent.CorrelationId.ToString(),
        payload.TodoListId.ToString(),
        payload.Id.ToString(),
        new UpdateExternalTodoItemRequest(payload.Name, payload.IsComplete),
        ct
    )
    .ConfigureAwait(false);
```

`TodoItemDeletedStrategy.cs`:

```csharp
await client
    .DeleteTodoItemAsync(
        syncEvent.CorrelationId.ToString(),
        payload.TodoListId.ToString(),
        payload.Id.ToString(),
        ct
    )
    .ConfigureAwait(false);
```

- [ ] **Step 5: Update strategy tests that exist to include the header argument**

In `TodoListCreatedStrategyTests.cs`, update the call assertion to
include the correlation id argument:

```csharp
await _client.Received(1).CreateTodoListAsync(
    Arg.Is<string>(id => Guid.TryParse(id, out _)),
    Arg.Is<CreateExternalTodoListRequest>(r =>
        r.SourceId == id.ToString()
        && r.Name == "groceries"),
    Arg.Any<CancellationToken>());
```

And update the `Returns(...)` setup for the client similarly — the
method now takes an extra leading positional argument.

In `TodoListDeletedStrategyTests.cs`, update `DeleteTodoListAsync`
assertions to include the string header arg:

```csharp
await _client.Received(1).DeleteTodoListAsync(
    Arg.Any<string>(),
    id.ToString(),
    Arg.Any<CancellationToken>());
```

And update the `Returns<Task>(_ => throw ...)` setup argument list for
the 404 test to match the new signature.

- [ ] **Step 6: Document in NOTES.md**

Append a new section to `NOTES.md` (the file already exists at repo
root):

```markdown
## N. CorrelationId on outbound sync requests

Every `SyncEvent` carries a `CorrelationId` (`Guid`, assigned at
construction via `GuidV7.NewGuid()`). The outbound strategies send it
as the HTTP header `X-Correlation-Id` on every mutation call
(`POST /todolists`, `PATCH /todolists/{id}`, `DELETE /todolists/{id}`,
`PATCH /todolists/{id}/todoitems/{id}`, `DELETE /todolists/{id}/todoitems/{id}`).

Why: after `OutboundSyncJob` batches saves (see §N+1 below), a crash
between the external API call and the `SaveChangesAsync` that marks
the `SyncEvent` `Completed` will cause the same event to be replayed
on the next run. The `CorrelationId` is stable across replays —
identical across every retry of the same logical event — so an
external API that honours it can deduplicate. The external mock used
in this repo does not honour the header; it is ignored. The
convention is defensive: against a real external, it buys us
at-most-once semantics without a distributed transaction.

The field is named `CorrelationId` locally for consistency with the
conversation it grew out of. Semantically it behaves like the
`Idempotency-Key` header popularized by Stripe; we send it as
`X-Correlation-Id` rather than `Idempotency-Key` to match the field
name 1:1. If we ever sync with a provider that requires
`Idempotency-Key` specifically, we rename the header in one line of
the Refit interface — the local field name stays.
```

(Pick `N` based on the current numbering at the end of `NOTES.md`.)

- [ ] **Step 7: Build + test**

```bash
dotnet build --nologo && dotnet test --nologo
```

Expected: green.

- [ ] **Step 8: Commit**

```bash
git add TodoApi NOTES.md TodoApi.Tests
git commit -m "feat(sync): add CorrelationId to SyncEvent, send as X-Correlation-Id

Every SyncEvent carries a Guid v7 CorrelationId, assigned at
construction. Strategies forward it as the X-Correlation-Id HTTP
header on every outbound mutation so a downstream API that honors
it can deduplicate replays that follow a mid-batch crash."
```

---

## Task 14: Batch `OutboundSyncJob` saves (constant `DefaultBatchSize = 10`) + NOTES.md

Current `OutboundSyncJob` calls `SaveChangesAsync` inside a `finally`
after every event — a round-trip per event. Switch to flushing every
`DefaultBatchSize` events, plus a final flush for the remainder.

**Files:**
- Modify: `TodoApi/Application/Jobs/OutboundSyncJob.cs`
- Modify: `NOTES.md`

- [ ] **Step 1: Rewrite `OutboundSyncJob.Execute`**

Replace the body of `TodoApi/Application/Jobs/OutboundSyncJob.cs` (only
`Execute` changes; the logger definition stays):

```csharp
[DisallowConcurrentExecution]
public sealed class OutboundSyncJob(
    IServiceScopeFactory scopeFactory,
    IHubContext<NotificationHub> hub,
    IClock clock,
    ILogger<OutboundSyncJob> logger
) : IJob
{
    private const int DefaultBatchSize = 10;

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await using var scope = scopeFactory.CreateAsyncScope();

        var syncEventRepo = scope.ServiceProvider.GetRequiredService<ISyncEventRepository>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<SyncEventDispatcher>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var pending = await syncEventRepo.GetPendingAsync(50, context.CancellationToken).ConfigureAwait(false);
        var coalesced = Coalesce(pending);
        var processed = 0;
        var failed = 0;
        var sinceLastFlush = 0;

        foreach (var evt in coalesced)
        {
            try
            {
                await dispatcher.DispatchAsync(evt, context.CancellationToken).ConfigureAwait(false);
                evt.MarkCompleted(clock.UtcNow);
                processed++;
            }
            catch (Exception ex)
            {
                evt.MarkFailed(ex.Message, clock.UtcNow);
                failed++;
                logger.LogOutboundSyncFailed(ex, evt.EntityType, evt.EventType, evt.EntityId);
            }

            sinceLastFlush++;
            if (sinceLastFlush >= DefaultBatchSize)
            {
                await uow.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
                sinceLastFlush = 0;
            }
        }

        if (sinceLastFlush > 0)
        {
            await uow.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
        }

        if (processed + failed > 0)
        {
            await hub
                .Clients.All.SendAsync(
                    "OutboundSyncJob",
                    new { Processed = processed, Failed = failed },
                    context.CancellationToken
                )
                .ConfigureAwait(false);
        }
    }
```

`Coalesce` method below is unchanged.

- [ ] **Step 2: Document batching in NOTES.md**

Append to `NOTES.md`:

```markdown
## N. OutboundSyncJob batching

`OutboundSyncJob` flushes `SaveChangesAsync` every
`DefaultBatchSize = 10` processed events (success or failure), plus a
final flush for any remainder. The previous implementation flushed
after every event — one DB round-trip per external API call.

Trade-off: if the process crashes after making external API calls
but before the batch flush, those events remain `Pending` in the DB
and will be re-dispatched on the next run. We accept this because:

1. The external API is expected to dedupe on `X-Correlation-Id`
   (see §N-1). Replays are therefore at-most-once from the API's
   point of view.
2. The mock external API used in dev does **not** dedupe — in dev a
   crash mid-batch can produce duplicate external rows. This is an
   acknowledged trade against I/O cost; not a correctness concern
   when running against an idempotent-honoring production API.

The batch size moves to the `ProcessOptions.BatchSize` configuration
value in the next step (see §N+1 / Task 15 in the plan); the
constant is a temporary stepping stone.
```

- [ ] **Step 3: Build + test**

```bash
dotnet build --nologo && dotnet test --nologo
```

Expected: green. `OutboundSyncJobTests` may need a minor update if it
asserted on exact `SaveChangesAsync` call counts — adjust to tolerate
the new batching shape.

- [ ] **Step 4: Commit**

```bash
git add TodoApi/Application/Jobs/OutboundSyncJob.cs NOTES.md TodoApi.Tests/Application/Jobs/OutboundSyncJobTests.cs
git commit -m "perf(sync): batch OutboundSyncJob saves every 10 events

Flushes SaveChangesAsync every DefaultBatchSize=10 processed events
plus a final flush for the remainder. Replays of uncommitted events
rely on X-Correlation-Id for external-side deduplication."
```

---

## Task 15: Replace `DefaultBatchSize` constant with `ProcessOptions` (Options pattern)

**Files:**
- Create: `TodoApi/Infrastructure/Settings/ProcessOptions.cs`
- Modify: `TodoApi/Infrastructure/Configuration/ApplicationExtensions.cs`
- Modify: `TodoApi/Application/Jobs/OutboundSyncJob.cs`
- Modify: `TodoApi/appsettings.json` and `TodoApi/appsettings.Development.json`
- Modify: `TodoApi/Program.cs` (if the `AddApplication` call site needs a new arg)

- [ ] **Step 1: Create the `Infrastructure/Settings` folder and `ProcessOptions`**

```bash
mkdir -p TodoApi/Infrastructure/Settings
```

Create `TodoApi/Infrastructure/Settings/ProcessOptions.cs`:

```csharp
namespace TodoApi.Infrastructure.Settings;

public sealed class ProcessOptions
{
    public const string SectionName = "Process";

    public int BatchSize { get; set; } = 10;
}
```

- [ ] **Step 2: Bind `ProcessOptions` in `ApplicationExtensions`**

Edit `TodoApi/Infrastructure/Configuration/ApplicationExtensions.cs`.
The current signature is `AddApplication(this IServiceCollection)`.
Extend it to take `IConfiguration` (matching `AddPersistence`):

```csharp
using TodoApi.Application.Services;
using TodoApi.Infrastructure.Settings;

namespace TodoApi.Infrastructure.Configuration;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSingleton<IBulkOperationTracker, BulkOperationTracker>();

        services.Configure<ProcessOptions>(configuration.GetSection(ProcessOptions.SectionName));

        return services;
    }
}
```

- [ ] **Step 3: Update the `AddApplication` caller**

Open `TodoApi/Program.cs`. Find the `AddApplication()` call. Change
to `AddApplication(builder.Configuration)`. If the existing call site
already uses `builder.Configuration` as a local variable, adjust
accordingly — the goal is to pass the `IConfiguration` in.

- [ ] **Step 4: Inject `IOptions<ProcessOptions>` into `OutboundSyncJob`**

Edit `TodoApi/Application/Jobs/OutboundSyncJob.cs`. Change the
primary constructor and drop the `DefaultBatchSize` constant:

```csharp
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Quartz;
using TodoApi.Application.Sync;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Hubs;
using TodoApi.Infrastructure.Persistence;
using TodoApi.Infrastructure.Settings;

namespace TodoApi.Application.Jobs;

[DisallowConcurrentExecution]
public sealed class OutboundSyncJob(
    IServiceScopeFactory scopeFactory,
    IHubContext<NotificationHub> hub,
    IClock clock,
    IOptions<ProcessOptions> options,
    ILogger<OutboundSyncJob> logger
) : IJob
{
    private readonly int _batchSize = options.Value.BatchSize;

    public async Task Execute(IJobExecutionContext context)
    {
        // ... body unchanged except DefaultBatchSize -> _batchSize ...
    }
```

Replace every `DefaultBatchSize` inside the method with `_batchSize`.
Remove the `private const int DefaultBatchSize = 10;` line entirely.

- [ ] **Step 5: Add `Process.BatchSize` to `appsettings.json`**

Add to `TodoApi/appsettings.json` (pretty-print matching existing
indentation; exact JSON structure depends on current file):

```json
"Process": {
  "BatchSize": 10
}
```

Also add to `TodoApi/appsettings.Development.json` if dev uses a
different value — otherwise skip (default in code is 10).

- [ ] **Step 6: Build + test**

```bash
dotnet build --nologo && dotnet test --nologo
```

Expected: green. Tests that construct `OutboundSyncJob` directly (if
any) need `Options.Create(new ProcessOptions { BatchSize = 10 })` as
the new ctor argument.

- [ ] **Step 7: Commit**

```bash
git add TodoApi TodoApi.Tests
git commit -m "refactor(sync): ProcessOptions replaces DefaultBatchSize constant

Batch size moves from a hard-coded constant in OutboundSyncJob to
ProcessOptions.BatchSize, bound via the Options pattern in
ApplicationExtensions from the 'Process' configuration section."
```

---

## Task 16: Regenerate migration

With the entity model now free of `SyncMapping`, delete the two
branch-local migrations and regenerate one clean migration that
creates the SyncEvent table, adjusts the TodoItem PK to `Guid`, adds
`CreatedAt`/`Order` on `TodoItem`, adds `CreatedAt` on `TodoList`, and
adds `ExternalId` on both.

**Files:**
- Delete: `TodoApi/Migrations/20260421130419_AddSyncModule.cs` + `.Designer.cs`
- Delete: `TodoApi/Migrations/20260422022609_TodoItemGuidPk.cs` + `.Designer.cs`
- Delete: `TodoApi/Migrations/TodoContextModelSnapshot.cs`
- Regenerate: `TodoApi/Migrations/<timestamp>_AddSyncModule.cs` + `.Designer.cs` + `TodoContextModelSnapshot.cs`

- [ ] **Step 1: Delete branch-local migrations and snapshot**

```bash
git rm TodoApi/Migrations/20260421130419_AddSyncModule.cs \
       TodoApi/Migrations/20260421130419_AddSyncModule.Designer.cs \
       TodoApi/Migrations/20260422022609_TodoItemGuidPk.cs \
       TodoApi/Migrations/20260422022609_TodoItemGuidPk.Designer.cs \
       TodoApi/Migrations/TodoContextModelSnapshot.cs
```

- [ ] **Step 2: Regenerate migration**

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations add AddSyncModule --project TodoApi
```

Expected: new migration file under `TodoApi/Migrations/`, plus its
`.Designer.cs` and a fresh `TodoContextModelSnapshot.cs`. Inspect the
generated `Up`:
- Creates `SyncEvent` table **with a `CorrelationId uniqueidentifier NOT NULL` column** (added by Task 13).
- Adds `ExternalId nvarchar(500) NULL` + filtered unique index on `TodoList` and `TodoItem`.
- Adds `CreatedAt` on `TodoList`.
- Changes `TodoItem.Id` from `bigint` to `uniqueidentifier` (if not already applied at DB level via earlier migrations that have been run — see Step 3).
- Adds `CreatedAt`, `Order` on `TodoItem`.
- No `SyncMapping` table.

If the generated file looks wrong, fix the model, not the migration.

- [ ] **Step 3: Decide DB reset vs migration path**

This branch's DB may already have `SyncMapping` applied from the old
migration. Two options:

a) Dev: drop and recreate the DB.

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet ef database drop --force --project TodoApi
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --project TodoApi
```

b) Hand-edit the regenerated migration to also `DropTable("SyncMapping")`
at the top of `Up`. Only needed if the dev DB cannot be recreated.

Default: option (a). If the task executor does not have DB access,
they stop here and surface the choice to the user.

- [ ] **Step 4: Build + test**

```bash
dotnet build --nologo && dotnet test --nologo
```

Expected: both green.

- [ ] **Step 5: Format**

```bash
dotnet csharpier .
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore(ef): regenerate AddSyncModule migration without SyncMapping

Collapses the branch-local AddSyncModule + TodoItemGuidPk migrations
into a single regenerated migration that matches the current model:
TodoList.ExternalId, TodoItem.ExternalId, TodoItem.Id as Guid,
CreatedAt/Order additions, SyncEvent table. SyncMappings table is
gone."
```

---

## Task 17: Final verification

- [ ] **Step 1: Clean build, no warnings**

```bash
dotnet clean && dotnet build --nologo
```

Expected: 0 errors, 0 warnings (TreatWarningsAsErrors fails the build otherwise).

- [ ] **Step 2: Full test suite**

```bash
dotnet test --nologo
```

Expected: all green.

- [ ] **Step 3: Grep for leftovers**

```bash
grep -rn "SyncMapping\|ISyncMappingRepository" TodoApi TodoApi.Tests ExternalApiMock || echo "OK: no leftover references"
```

Expected: `OK: no leftover references`.

- [ ] **Step 4: Run the external integration tests locally (manual)**

The public contract is validated by
`https://github.com/crunchloop/interview-tests`. Run them against the
API after starting it:

```bash
dotnet run --project TodoApi &
# run interview-tests per their README
```

Expected: they pass — the outbound create path writes `ExternalId` and
subsequent update/delete flows resolve through either `ExternalId` or
`SourceId` fallback.

(If the executor cannot run the interview-tests locally, record that
they were not run and flag to the user.)

---

## Follow-up / not done here

- Add unit tests for `TodoListUpdatedStrategy`, `TodoItemCreatedStrategy`,
  `TodoItemUpdatedStrategy`, `TodoItemDeletedStrategy`, and
  `InboundSyncJob`. None exist today; adding them is orthogonal to the
  refactor but fills a real gap against ADR-0011.
- Paginate `client.GetAllAsync` — user-flagged improvement.
- Delta-cursor pulls: reintroduce a per-collection `SyncCursor` row
  (not per-entity `ExternalUpdatedAt`) if full-replace becomes too
  expensive.
- Consider whether externally-injected items should ever be mapped
  back to a local `Order` that makes sense to the user, or flagged as
  external-origin in the UI.
