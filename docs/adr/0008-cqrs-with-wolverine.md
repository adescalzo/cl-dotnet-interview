# 0008 - CQRS dispatched via Wolverine (supersedes ADR-0004)

- Status: accepted
- Date: 2026-04-19
- Deciders: TodoApi team
- Supersedes: ADR-0004

## Context and problem statement

ADR-0004 picked **Kommand** as the CQRS dispatcher. After a closer
look the choice did not hold up:

- Only a `1.0.0-alpha.1` prerelease is published on NuGet. Pinning an
  architectural decision to a prerelease from a single maintainer is
  a supply-chain risk we should not take on day one.
- The library is effectively unproven — the download counter reflects
  that, and there is no ecosystem around it (no validation middleware,
  no integration-tests harness, no observability package).
- We already want the door open for sync work later (outbox, scheduled
  jobs, durable messaging). Kommand does not cover that; we would end
  up stacking another library on top when sync arrives.

We still want what ADR-0004 wanted: one named type per use case,
thin handlers, and a place to hook pipelines (validation, logging,
transactions).

## Decision drivers

- Replace a prerelease-only dependency with a production-grade one.
- Keep the "one folder per use case" shape from ADR-0004 so no
  handler code has to change structurally.
- Pick a library whose surface area covers what comes next (domain
  events, scheduled jobs, outbox) without swapping mediators again.
- Keep handler code free of framework noise — handlers stay POCOs,
  business rules on the aggregate (ADR-0003).
- First-class observability and FluentValidation integration out of
  the box.

## Considered options

- **Wolverine (WolverineFx).** In-process + durable messaging, CQRS
  mediator, domain events, scheduled jobs, OpenTelemetry, outbox,
  first-party FluentValidation middleware.
- **Stay with Kommand.** Minimal mediator, prerelease only.
- **MediatR.** Well-known mediator; licensing change in 2024 moved it
  to a commercial track for organizations above a revenue threshold.
- **Hand-rolled dispatcher.** Plain DI + one interface. No ecosystem.

## Decision outcome

Chosen option: **Wolverine (WolverineFx)**, wired as the in-process
CQRS dispatcher. Durable messaging, outbox, and scheduled jobs stay
available but unused until a concrete requirement lands (sync is
explicitly deferred — see CLAUDE.md).

Conventions (replaces ADR-0004 conventions):

- One folder per use case under
  `Application/Commands/<UseCase>/` or `Application/Queries/<UseCase>/`.
  The folder contains the command/query record, the handler class,
  and an optional validator.
- Commands and queries are plain records. Names are imperative for
  commands (`CreateTodoList`) and noun-shaped for queries
  (`GetTodoListById`). **No marker interface** — Wolverine discovers
  handlers by convention (class name suffix `Handler` and method name
  `Handle` / `HandleAsync`).
- Handlers are **thin POCOs**: load aggregate(s) via repository, call
  domain methods, persist, return. Business rules live on the
  aggregate (ADR-0003), never in the handler.
- Handlers return `Result` / `Result<T>` (ADR-0006). They do not throw
  for expected failures.
- Controllers / endpoints dispatch via `IMessageBus.InvokeAsync<T>(...)`
  and translate `Result → HTTP` via the shared helper (ADR-0007).
- Cross-cutting concerns use Wolverine **middleware**:
  - Validation: `WolverineFx.FluentValidation` middleware converts a
    validator's failures into `Result` with
    `Error.Category = Validation` — it does **not** throw. (When we
    add it, an ADR will note the glue code.)
  - Logging / tracing: Wolverine's built-in OpenTelemetry support is
    enabled; per-handler logging is added only when a second handler
    needs the same behavior.
  - Transactions: Wolverine's `TransactionalMiddleware` or the
    `[Transactional]` attribute once a handler crosses aggregates.
- Domain events raised by aggregates (ADR-0003) are dispatched via
  Wolverine after `SaveChangesAsync` returns — we use
  `CascadeMessages` on handlers rather than a hand-rolled dispatcher.

### Consequences

- Positive: mature library with a broader surface area (outbox,
  scheduler, OTel) so the first sync ADR does not have to re-pick a
  mediator.
- Positive: no marker interfaces — commands and queries stay framework-
  clean records. CA1040's exemption in `.editorconfig` is no longer
  load-bearing; we leave it disabled anyway (empty marker interfaces
  may still appear, e.g. DI tags, and re-enabling is out of scope).
- Positive: FluentValidation integration exists as a first-party
  package, so validation still flows through `Result<T>` (ADR-0006)
  rather than exceptions.
- Positive: OpenTelemetry support aligns with our logging/tracing
  plans (ADR-0009).
- Negative: Wolverine is heavier than Kommand. We accept the weight —
  we get an ecosystem in return.
- Negative: convention-based handler discovery means a handler with
  the wrong method name is silently not discovered. Mitigated by
  integration tests and, if it becomes a recurring trap, a
  startup-time sanity check that every command type has a handler.
- Neutral: the "one folder per use case" shape stays — no structural
  change to handler files.

## Links

- Supersedes: ADR-0004 (Kommand).
- Builds on: ADR-0002 (layering), ADR-0003 (aggregate-owned behavior).
- Related: ADR-0006 (`Result` flows through middleware too, no
  exceptions), ADR-0007 (HTTP mapping unchanged), ADR-0009 (Serilog +
  Wolverine OTel share a pipeline).
- Library: <https://wolverinefx.net> / <https://github.com/JasperFx/wolverine>.
