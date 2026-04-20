# CLAUDE.md

Guidance for Claude Code (claude.ai/code) when working in this repository.

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
dotnet test --filter "FullyQualifiedName~TodoListsControllerTests"
dotnet test --filter "FullyQualifiedName~TodoListsControllerTests.GetTodoList_WhenCalled_ReturnsTodoListList"

# Format (CSharpier)
dotnet csharpier .

# EF Core migrations (run from repo root; project flag points at the API)
dotnet ef migrations add <Name> --project TodoApi
dotnet ef database update --project TodoApi
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

### CQRS with Kommand

We use the **Kommand** library (<https://github.com/Atherio-Ltd/Kommand>)
as our CQRS dispatcher. Add it to `Directory.Packages.props` once and
reference it from each module's `Application` project.

Conventions:

- `Application/Commands/<UseCase>/` contains the command record, the
  handler, and (optionally) a validator. One folder per use case.
- `Application/Queries/<UseCase>/` mirrors the same shape for reads.
- Commands return `Result` / `Result<T>` — no throwing for expected
  failures (validation, not-found, conflict). Throw only for truly
  exceptional conditions.
- Handlers are **thin**: load aggregate(s), call domain methods, persist,
  return. Business rules live in the domain, not the handler.
- Controllers/endpoints translate HTTP → command/query, dispatch via
  Kommand, translate result → HTTP via the shared `Result → HTTP`
  helper that renders failures as `ProblemDetails` (see
  "API error responses" below). No business logic in controllers.
- The marker interfaces `ICommand` / `IQuery` are already accepted by
  `.editorconfig` (CA1040 is disabled with a comment pointing at this
  pattern).

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
- A single shared helper (`result.ToActionResult()` for controllers /
  `result.ToHttpResult()` for Minimal APIs) maps
  `Result.Error.Category` → HTTP status:
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
- CA1040 (empty interfaces) is disabled because of CQRS marker
  interfaces — that exemption is intentional, do not re-enable.
- Several CA/Sonar rules are downgraded to `suggestion` for incremental
  cleanup. Do not silence new rules without an ADR.

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
| 0004 | CQRS as application pattern, dispatched via Kommand          | One folder per use case; thin handlers; Kommand library |
| 0005 | Entity Framework Core for persistence, one DbContext / module| Per-module `DbContext`, migrations and configurations   |
| 0006 | Use `Result<T>` for expected failures                        | No exceptions for validation/not-found/conflict         |
| 0007 | Use RFC 7807 `ProblemDetails` for HTTP error responses       | Single error format, mapped from `Error.Category`       |

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
