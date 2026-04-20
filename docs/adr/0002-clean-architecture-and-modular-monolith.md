# 0002 - Adopt Clean Architecture and Modular Monolith

- Status: accepted
- Date: 2026-04-19
- Deciders: TodoApi team

## Context and problem statement

The starter codebase has controllers calling `DbContext` directly,
DTOs in a flat folder, and anemic models. As the API grows
(authentication, multiple resources, eventually external sync) this
shape will not hold: business rules will end up scattered across
controllers, persistence concerns will leak into HTTP code, and
adding a new bounded context will mean editing every layer at once.

We need a structure that:

- Keeps business rules independent from frameworks (HTTP, EF Core).
- Lets us add new bounded contexts without touching unrelated code.
- Supports incremental migration — we cannot rewrite the whole API in
  one PR.
- Is recognizable to .NET developers without inventing new vocabulary.

## Decision drivers

- The challenge is small enough that microservices are overkill, but
  large enough that one undifferentiated project will rot.
- We want to add modules (Authentication, TodoLists, …) one at a time.
- Future sync work needs a clean place for outbox/integration code
  without polluting the domain.
- Tests should be able to target the domain and application layers
  without spinning up the web host.

## Considered options

- **Clean Architecture inside a Modular Monolith.** Each bounded
  context is a module with its own `Domain` / `Application` /
  `Infrastructure` / `Api` layering. One deployable, multiple internal
  modules.
- **Plain Clean Architecture, one module.** Three projects
  (`Domain` / `Application` / `Infrastructure`) for the whole API, no
  internal module split.
- **Vertical slice architecture.** Organize by feature, not by layer.
- **Stay with the starter layout (controllers + DbContext).**

## Decision outcome

Chosen option: **Clean Architecture inside a Modular Monolith**.

Each bounded context lives at `TodoApi.Modules.<Name>/` with the
four-layer split:

```
Domain          — entities, value objects, aggregates, domain events,
                  repository interfaces. Zero outward dependencies.
Application     — commands, queries, handlers, port interfaces.
                  Depends only on Domain.
Infrastructure  — EF Core configurations, repository implementations,
                  external integrations. Depends on Domain.
Api             — controllers / endpoints. Depends on Application.
```

Dependency rule: `Api → Application → Domain ← Infrastructure`. Domain
depends on nothing. Cross-module communication uses application-level
contracts (commands, queries, integration events), never direct
references to another module's `Domain` or `Infrastructure`.

A small shared kernel is allowed for primitives only (`Result<T>`,
`Error`, base `Entity` / `ValueObject`, ids). Domain concepts go in a
module, not in the shared kernel.

### Consequences

- Positive: each module can be reasoned about, tested, and refactored
  in isolation.
- Positive: extracting a module into its own service later becomes a
  mechanical change instead of a redesign.
- Positive: layer boundaries make analyzer rules and DI registration
  cleaner — Scrutor can scan per module.
- Negative: more projects/folders than the starter; small modules
  carry overhead that does not pay off until the second feature is
  added. We mitigate this by **not** pre-creating empty modules.
- Negative: contributors unfamiliar with Clean Architecture pay a
  learning cost on the first PR.

## Links

- Supersedes the implicit "controllers + DbContext" layout from the
  starter template.
- Related: ADR-0003 (DDD building blocks), ADR-0004 (CQRS via
  Kommand), ADR-0005 (EF Core for persistence).
