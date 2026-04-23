# CLAUDE.md

Guidance for Claude Code (claude.ai/code) when working in this repository.

## Collaboration style

- **Don't be condescending.** No "great question!", "you're absolutely
  right!", "excellent point!", no praise for ordinary requests. Skip
  the compliments and answer.
- **Be critical of ideas and proposals** — mine and yours. If a
  suggestion has a flaw, a hidden cost, a wrong assumption, or a
  better alternative, say so plainly before executing. Agreement by
  default is not helpful; it just ships worse code.
- Push back with reasons, not with hedges. "This breaks X because Y"
  beats "you might want to consider…".
- If you end up disagreeing with a directive and still proceeding,
  call out the disagreement explicitly so the decision is visible
  and reversible.

## Current focus

We are **closing the API and frontend architecture first**. Sync between
this API and the external system is **deferred** and out of scope for
now — do not start sync work, do not add sync packages, do not add
sync-related abstractions. If a request seems to push toward sync,
stop and ask.

Order of work, in this order, no jumping ahead:

1. Lock down the API architecture (this repo).
2. Close pending items on the React frontend (separate repo).
3. Then, and only then, design and implement sync.

## Commands

```bash
# Build (warnings are errors — see Directory.Build.props)
dotnet build

# Run API (SQL Server connection string in appsettings.Development.json)
dotnet run --project TodoApi

# All tests
dotnet test

# Single test class / method
dotnet test --filter "FullyQualifiedName~CreateTodoListHandlerTests"
dotnet test --filter "FullyQualifiedName~CreateTodoListHandlerTests.Handle_WhenCalled_ShouldPersistTodoListAndReturnResponse"

# Format (CSharpier)
dotnet csharpier .

# EF Core migrations (run from repo root; project flag points at the API)
ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations add <Name> --project TodoApi
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --project TodoApi
```

## Treat the existing code as starter scaffolding, not a base

The code currently in `TodoApi/` (controllers calling `TodoContext`
directly, anemic `Models/`, `Dtos/` folder, etc.) is the **starter
layout from the interview template**. It is **not** the base we are
building on. Do not extend it, do not preserve its shape, do not add
new endpoints next to the existing ones.

The target architecture below replaces it. Existing files get **moved,
rewritten, or deleted** as each module is introduced. Migrations and
the `TodoContext` get reorganized under the first module's
`Infrastructure` and renamed accordingly. When in doubt about what to
keep, the answer is: keep the build settings (`Directory.Build.props`,
`Directory.Packages.props`, `.editorconfig`, solution file) and
replace everything else.

External integration tests live at
<https://github.com/crunchloop/interview-tests> and run against the API
over HTTP — the public contract those tests expect (resource URLs,
payload shapes, status codes) is the constraint that the new
implementation must still satisfy. Read those tests before reshaping
endpoints.

## Target architecture

We are migrating the codebase toward a **modular monolith** organized
by **Clean Architecture** layers, with **DDD** building blocks inside
each module and **CQRS** as the application-layer pattern. Adopt these
incrementally, one module at a time. Do **not** rewrite the whole repo
in one PR — work module-by-module and document each step with an ADR
(see below).

### Layering (per module)

```
TodoApi.Modules.<ModuleName>/
  Domain/           — entities, value objects, domain events, aggregates,
                       domain services, repository interfaces
  Application/      — commands, queries, handlers, DTOs, validators,
                       application services, port interfaces
  Infrastructure/   — EF Core configurations, repository implementations,
                       integrations, persistence
  Api/ (optional)   — controllers / endpoints exposing this module
```

Dependency rule, no exceptions:
`Api → Application → Domain ← Infrastructure`. Domain depends on
nothing. Infrastructure depends on Domain to implement ports.
Application orchestrates and depends on Domain. Api depends on
Application (and DTOs, never on Domain entities directly).

### Modular monolith boundaries

- One folder/project per bounded context. Start with `TodoLists` as the
  first module; do not pre-create empty modules.
- Cross-module communication goes through **application-level
  contracts** (commands/queries/events), never by reaching into another
  module's `Domain` or `Infrastructure`.
- Shared kernel is allowed only for genuinely shared primitives
  (`Result<T>`, `Error`, base `Entity`/`ValueObject`, ids). Keep it
  small. If you are tempted to put a domain concept there, it belongs
  in a module.
- Use **Scrutor** (already in `Directory.Packages.props`) to scan and
  register handlers/services per module — avoid one giant DI
  registration block.

### DDD building blocks

- **Entities** with identity and behavior (no anemic models — methods
  live on the entity, not in services).
- **Value objects** for concepts without identity; immutable; equality
  by value.
- **Aggregates** with a single root that enforces invariants.
  Repositories are per aggregate root, not per table.
- **Domain events** raised from aggregates; dispatched by the
  application layer after the unit of work commits.
- **Repositories** are interfaces in `Domain`; EF Core implementations
  live in `Infrastructure`.
- **Specifications** for non-trivial queries that belong to the domain
  vocabulary.

### CQRS with Wolverine

We use **Wolverine** (<https://wolverinefx.net>) as our CQRS
dispatcher and in-process message bus. Packaged as `WolverineFx` in
`Directory.Packages.props`. See ADR-0008 (which supersedes ADR-0004's
Kommand choice).

Conventions:

- `Application/Commands/<UseCase>/` contains the command record, the
  handler class, and (optionally) a validator. One folder per use
  case.
- `Application/Queries/<UseCase>/` mirrors the same shape for reads.
- Commands and queries are plain records — **no marker interface**.
  Wolverine discovers handlers by convention: a class whose name ends
  in `Handler` with a `Handle` / `HandleAsync` method taking the
  message type. Keep handlers in `Application/Commands|Queries/…`
  so the auto-scan picks them up.
- Handlers return `Result` / `Result<T>` — no throwing for expected
  failures (validation, not-found, conflict). Throw only for truly
  exceptional conditions (ADR-0006).
- Handlers are **thin POCOs**: load aggregate(s), call domain methods,
  persist, return. Business rules live in the domain, not the handler.
- Handlers **do not inject `TodoContext`** and **do not call
  `SaveChangesAsync` explicitly**. Persistence is flushed by the
  Wolverine `TransactionMiddleware.Finally` (see
  `TodoApi/Infrastructure/Mediator/TransactionMiddleware.cs`), which
  runs `SaveChangesAsync` on the tracked `TodoContext` when
  `ChangeTracker.HasChanges()` is true. Consequence: response values
  must be derivable from state known **before** the save — e.g. IDs
  assigned client-side (`GuidV7` on aggregates) or inputs from the
  command. Do not design a response that depends on DB-generated
  values materialising inside the handler body.
- Controllers/endpoints translate HTTP → command/query, dispatch via
  `IMessageBus.InvokeAsync<T>(...)`, translate result → HTTP via the
  shared `Result → HTTP` helper that renders failures as
  `ProblemDetails` (see "API error responses" below). No business
  logic in controllers.
- Cross-cutting concerns use **Wolverine middleware**:
  - FluentValidation via `WolverineFx.FluentValidation`, wired so that
    validator failures become a `Result` with
    `Error.Definition = Validation`. Do **not** throw `ValidationException`
    from handlers or middleware (ADR-0006).
  - Transactions via Wolverine's `TransactionalMiddleware` /
    `[Transactional]` once a handler crosses aggregates.
  - Logging/tracing via Wolverine's OpenTelemetry support (ADR-0009).
- Domain events raised by aggregates (ADR-0003) are published via
  Wolverine (`CascadeMessages` on the handler) after
  `SaveChangesAsync` returns — no hand-rolled dispatcher.

### Entity Framework Core

- One `DbContext` per module, scoped to that module's aggregates.
- Configuration goes in `Infrastructure/Persistence/Configurations/`
  using `IEntityTypeConfiguration<T>`. No Fluent API in `OnModelCreating`
  beyond `ApplyConfigurationsFromAssembly`.
- Migrations belong to the module that owns the `DbContext`.
- `InMemory` provider stays for unit tests. Integration tests that need
  real SQL semantics go against SQL Server (devcontainer provisions it).
- Repository implementations expose **aggregate-shaped** APIs, not
  generic `IRepository<T>`. Avoid leaking `IQueryable` outside
  `Infrastructure`.

### API error responses (RFC 7807 ProblemDetails)

All HTTP failure responses are rendered as **`ProblemDetails`** /
`ValidationProblemDetails` (`application/problem+json`, RFC 7807).
This is the only error format the API speaks — no ad-hoc
`{ "error": "..." }` envelopes, no plain-text error bodies.

Conventions:

- Enable framework support in `Program.cs` via `AddProblemDetails`,
  `UseExceptionHandler`, `UseStatusCodePages`. Do not hand-roll
  middleware that duplicates that.
- Shared helpers in `TodoApi.Infrastructure.Extensions.ResultExtensions`
  (`.ToOk()`, `.ToCreated()`, `.ToNoContent()`, `.ToProblemDetails()`)
  map `Result.Error.Definition` → HTTP status:
  `Validation`→400, `Unauthorized`→401, `Forbidden`→403,
  `NotFound`→404, `Conflict`→409, `Unexpected`→500.
- Body fields: `type` (stable URI per error code, even if it does not
  resolve), `title`, `status`, `detail` (= `Error.Message`),
  `instance` (request path, automatic), and the extensions `code`
  (= `Error.Code` from the Result model) and `traceId`.
- Validation errors use `ValidationProblemDetails` with field-keyed
  `errors`.
- The global exception handler renders unhandled exceptions as a
  generic 500 `ProblemDetails`. **No** stack traces or internal
  details in the response body — those go to the log, correlated by
  `traceId`.
- Controllers do not build `ProblemDetails` by hand; they call the
  shared helper.

See ADR-0007.

### Logging (Serilog)

Logging goes through **Serilog** (`Serilog.AspNetCore` + Console /
File sinks). See ADR-0009. Highlights:

- `Program.cs` calls `UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration))`.
  All sink/enricher configuration lives in `appsettings*.json` under
  a `Serilog` section — do not hard-code sinks in C#.
- Required enrichers: `FromLogContext`, `WithMachineName`,
  `WithEnvironmentName`. The `TraceId` propagated via `LogContext`
  is the same one rendered in `ProblemDetails.traceId` (ADR-0007),
  so logs and error responses pivot against each other.
- Sinks: Console (dev loop), rolling File at `logs/todoapi-.log`
  (30-day retention).
- Use `ILogger<T>` in consumers. Do not reference `Log.Logger` or
  Serilog types outside the composition root.
- Do **not** log request/response bodies by default. Adding
  per-endpoint body logging requires explicit scope + redaction.

### Testing

Unit-test conventions for handlers and validators live in ADR-0011.
Highlights:

- Handler tests inherit `AsyncLifetimeBase` (EF Core `InMemory`) and
  construct the real handler against the real `DbContext` and
  repository — no mocked repositories. Outbound ports (e.g. `IClock`)
  are stubbed via `NSubstitute`.
- Test subjects are built through immutable `IBuilder<T>`
  implementations backed by `Bogus` (`TodoListBuilder`,
  `TodoItemBuilder`).
- Validator tests use `FluentValidation.TestHelper` directly — no base
  class.
- Assertions use `FluentAssertions` (v6). Naming:
  `Method_WhenCondition_ShouldOutcome`.
- Integration tests over HTTP live in `crunchloop/interview-tests` and
  are not duplicated in this repo.

### Patterns: use them where they pay rent

Apply patterns when the requirement justifies them. Pick the *simplest*
thing that satisfies the requirement and the architectural rules above.
Common ones we expect to use:

- Result/Either for error returns from handlers
- Specification for reusable query predicates
- Domain events + outbox if/when sync gets added (later)
- Decorator (via Scrutor) for cross-cutting concerns on handlers
  (logging, validation, transactions)

Do **not** add patterns "for symmetry" or "for the future." If only one
handler needs validation, validate inline; introduce the pipeline
decorator when the second handler appears.

## Standards

### `.editorconfig` is the source of truth

`.editorconfig` at repo root drives style and analyzer severity. If
guidance here ever conflicts with `.editorconfig`, follow
`.editorconfig` and update this file.

Highlights worth remembering:

- `TreatWarningsAsErrors=true` and `CodeAnalysisTreatWarningsAsErrors=true`
  in `Directory.Build.props` — analyzer warnings break the build.
- `AnalysisMode=All` with `SonarAnalyzer.CSharp` enabled.
- `var` is required (`csharp_style_var_*` = error).
- No `this.` qualification (`dotnet_style_qualification_for_*` = error).
- Use language keywords (`int`, not `Int32`).
- Expression-bodied properties/indexers/accessors required; methods and
  constructors stay block-bodied.
- Pattern matching preferred over `is`-cast / `as`-null-check (error).
- CA1040 (empty interfaces) is disabled. The original justification
  was CQRS marker interfaces; Wolverine does not require them
  (ADR-0008), so the exemption is dormant rather than load-bearing.
  Leave it disabled — re-enabling is out of scope for the Wolverine
  migration and would need its own ADR.
- Several CA/Sonar rules are downgraded to `suggestion` for incremental
  cleanup. Do not silence new rules without an ADR.

### Async conventions

- Append `.ConfigureAwait(false)` to **every** `await` inside library /
  application code (handlers, repositories, strategies, jobs, services).
  This tells the runtime not to capture and resume on the original
  `SynchronizationContext`, which avoids deadlocks in environments that
  have one (e.g. older ASP.NET, UI frameworks, unit-test runners) and
  removes a small allocation on every continuation.
- The exception is test code and top-level `Program.cs` startup where
  context capture is irrelevant or explicitly desired.
- Do **not** omit `.ConfigureAwait(false)` to "keep the code shorter" —
  it is a correctness and performance property, not style noise.

### High-performance logging

Use `[LoggerMessage]` source generation for every log call — never call
`logger.LogInformation(...)` with a string literal or interpolation directly.

```csharp
internal static partial class MyHandlerLoggerDefinition
{
    [LoggerMessage(
        EventId = 100,
        Level = LogLevel.Information,
        EventName = "TodoListCreated",
        Message = "TodoList created - Id: {Id}, Name: {Name}"
    )]
    public static partial void LogTodoListCreated(this ILogger logger, Guid id, string name);
}
```

Why:

- The compiler generates the log method at build time, eliminating boxing,
  string interpolation, and `params object[]` allocations on every call.
- Enforces structured logging — message template parameters are strongly
  typed in the generated code, not mixed with the message string at runtime.
- `EventId` provides a stable, queryable identifier across log sinks.
- `EventName` appears as a structured property, useful for filtering in any structured log sink.

Rules:

- One `internal static partial class <HandlerName>LoggerDefinition` per
  handler, placed at the bottom of the same file.
- `EventId` values must be unique across the codebase — maintain a registry
  in the handler files (100-series for TodoList, 500-series for TodoItem, etc.).
- Do **not** use string interpolation (`$"..."`) or concatenation in log
  messages; the template parameters handle structured values.
- Pass the concrete `ILogger<T>` instance; the extension method accepts
  `ILogger` so it works without casting.

### Package management

- Versions live centrally in `Directory.Packages.props`
  (`ManagePackageVersionsCentrally=true`). Project files reference
  packages without versions.
- When adding a package, update `Directory.Packages.props` first.

## Architecture Decision Records (ADRs)

Any architectural choice — module boundaries, picking a library,
choosing a persistence strategy, introducing a cross-cutting pattern —
gets an ADR **before** the code lands.

### Location and format

```
docs/adr/
  0001-record-architecture-decisions.md
  0002-adopt-clean-architecture-and-modular-monolith.md
  0003-cqrs-with-kommand.md
  ...
```

Use the **MADR** template (Markdown ADR), short form:

```markdown
# <NNNN> - <Title>

- Status: proposed | accepted | superseded by ADR-XXXX | deprecated
- Date: YYYY-MM-DD
- Deciders: <names>

## Context and problem statement
What is the situation? What forces are at play?

## Decision drivers
- <driver 1>
- <driver 2>

## Considered options
- Option A
- Option B
- Option C

## Decision outcome
Chosen option: "<A>", because <justification>.

### Consequences
- Positive: ...
- Negative: ...
- Neutral: ...

## Links
- Related ADRs, RFCs, issues, docs
```

### Rules for ADRs

- Numbered sequentially, never re-numbered. To overturn one, write a new
  ADR with status `accepted` that marks the old one `superseded by ADR-NNNN`.
- One decision per ADR. If a PR carries two decisions, write two ADRs.
- Keep them short — two screens max. If you need more, link out.
- ADR-0001 is always "Record architecture decisions" (the meta-ADR
  declaring we use ADRs). Add it as the first thing.

### When to write one

Write an ADR for: introducing/removing a library, changing a layer
boundary, picking a persistence pattern, adopting a cross-cutting
pattern (validation pipeline, outbox, auth scheme), changing how
modules communicate. Do **not** write ADRs for routine code — naming,
small refactors, bug fixes.

### Existing ADRs

The current architectural baseline is recorded in `docs/adr/`. Read
these before proposing changes that touch any of these areas:

| #    | Title                                                        | What it locks in                                        |
|------|--------------------------------------------------------------|---------------------------------------------------------|
| 0001 | Record architecture decisions                                | Use MADR short form, numbered, in `docs/adr/`           |
| 0002 | Adopt Clean Architecture and Modular Monolith                | Per-module `Domain/Application/Infrastructure/Api`      |
| 0003 | Use DDD building blocks inside each module                   | Entities, value objects, aggregates, repositories       |
| 0004 | ~~CQRS as application pattern, dispatched via Kommand~~      | Superseded by ADR-0008 (Wolverine)                      |
| 0005 | Entity Framework Core for persistence, one DbContext / module| Per-module `DbContext`, migrations and configurations   |
| 0006 | Use `Result<T>` for expected failures                        | No exceptions for validation/not-found/conflict         |
| 0007 | Use RFC 7807 `ProblemDetails` for HTTP error responses       | Single error format, mapped from `Error.Category`       |
| 0008 | CQRS dispatched via Wolverine (supersedes ADR-0004)          | WolverineFx for CQRS + in-process messaging; no markers |
| 0009 | Serilog for structured logging                               | `Serilog.AspNetCore` + Console / File sinks             |
| 0010 | Use UUIDv7 (Guid v7) for aggregate identifiers               | `GuidV7.NewGuid()` today; `Guid.CreateVersion7()` on net9+ |
| 0011 | Unit testing conventions for handlers and validators         | Bogus + immutable Builders + `AsyncLifetimeBase` (EF InMemory) |
| 0012 | Background scheduling with Quartz.NET, SignalR, EF Core      | Quartz jobs, `IServiceScopeFactory`, `[DisallowConcurrentExecution]` |
| 0013 | Refit for typed external HTTP clients                        | `IExternalTodoApiClient` interface, snake_case, 404-on-DELETE |
| 0014 | Polly for HTTP resilience on external API calls              | Retry + circuit breaker on Refit `HttpClient`, per-entity isolation |
| 0015 | Strategy pattern for sync event dispatch                     | One `ISyncEventStrategy` per `(EntityType, EventType)`, OCP-compliant |
| 0016 | Replace SyncMapping table with ExternalId column on aggregates | Nullable `ExternalId` on TodoList/TodoItem; drop SyncMapping        |

When you write the next ADR, increment the number, add a row to this
table in the same PR, and link it from any related sections above.

## What not to do

- Do not start sync work or add sync-shaped abstractions yet.
- Do not introduce a new pattern without an ADR.
- Do not silence analyzer rules to make a warning go away — fix the
  code, or write an ADR explaining why the rule is being downgraded.
- Do not put business logic in controllers or handlers — push it to the
  domain.
- Do not create empty modules / projects "for later." Add structure
  when a real use case needs it.
- Do not bypass `Directory.Packages.props` by hard-coding versions in
  a `.csproj`.
- Do not preserve the starter `Controllers/`, `Models/`, `Dtos/`, or
  `Data/TodoContext.cs` as-is. They are scaffolding; replace them as
  the new modules come in.
