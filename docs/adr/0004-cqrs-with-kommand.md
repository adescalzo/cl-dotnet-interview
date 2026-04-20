# 0004 - CQRS as application pattern, dispatched via Kommand

- Status: superseded by ADR-0008
- Date: 2026-04-19
- Deciders: TodoApi team

## Context and problem statement

Inside the `Application` layer of each module (ADR-0002), we need a
shape for use cases. The two natural candidates are application
services with one method per use case, or CQRS with one
command/query type per use case dispatched through a mediator.

We also need a concrete library — building a dispatcher in-house is
not justified for a project of this size, but the choice of library
sticks once handlers and pipeline behaviors are written against it.

## Decision drivers

- Each use case should be addressable as a single named type, easy to
  test in isolation and easy to wire into pipelines (validation,
  logging, transactions).
- The library must be lightweight and not lock us into a heavy
  framework.
- Marker interfaces (`ICommand`, `IQuery`) must be acceptable — the
  `.editorconfig` already disables CA1040 in anticipation of this
  pattern.
- Avoid coupling controllers to handler implementations; controllers
  dispatch a typed message and translate the result.

## Considered options

- **CQRS via the Kommand library** (<https://github.com/Atherio-Ltd/Kommand>).
- **CQRS via MediatR.** The widely-known option; recently changed
  licensing terms.
- **Application services, no mediator.** One service interface per
  module, methods per use case.
- **Endpoint-only handlers (Minimal APIs with feature folders).** No
  mediator, each endpoint is its own handler.

## Decision outcome

Chosen option: **CQRS via Kommand**.

Conventions:

- One folder per use case under
  `Application/Commands/<UseCase>/` or `Application/Queries/<UseCase>/`.
  The folder contains the command/query record, the handler, and an
  optional validator.
- Commands and queries are records. Names are imperative for commands
  (`CreateTodoList`) and noun-shaped for queries (`GetTodoListById`).
- Handlers are **thin**: load aggregate(s) via repository, call
  domain methods, persist, return. Business rules live on the
  aggregate (ADR-0003), never in the handler.
- Handlers return `Result` / `Result<T>` (ADR-0006). They do not throw
  for expected failures.
- Cross-cutting concerns (logging, validation, transaction scope) are
  added as decorators registered through Scrutor — only when a second
  handler needs the same behavior.
- Controllers / endpoints translate HTTP → command/query, dispatch
  via Kommand, translate `Result` → HTTP (typically a
  `ProblemDetails` response on failure, see ADR-0007).

### Consequences

- Positive: each use case is a single named type, testable without
  HTTP, with a stable signature.
- Positive: pipelines (validation, transactions, logging) can be added
  centrally without rewriting handlers.
- Positive: queries and commands are visibly separated, even though
  they share the same persistence today.
- Negative: dependency on a third-party library — if Kommand stops
  being maintained, we either fork or migrate. Mitigated by keeping
  handlers free of Kommand-specific code beyond the marker interface.
- Negative: more types per use case than an application service. We
  accept this; the indirection pays off as soon as pipeline behaviors
  are added.

## Links

- Builds on: ADR-0002 (layering), ADR-0003 (handlers stay thin
  because aggregates own behavior).
- Related: ADR-0006 (Result-based error handling), ADR-0007
  (ProblemDetails as the HTTP error format).
- Library: <https://github.com/Atherio-Ltd/Kommand>.
