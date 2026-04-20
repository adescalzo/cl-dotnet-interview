# 0003 - Use DDD building blocks inside each module

- Status: accepted
- Date: 2026-04-19
- Deciders: TodoApi team

## Context and problem statement

ADR-0002 settled on Clean Architecture inside a Modular Monolith. That
gives us layers and module boundaries, but it does not say *how* the
domain layer is shaped. Without an internal vocabulary, the `Domain`
folder will fill up with anemic data classes and "service" classes
that hold all the logic, which is what we are trying to move away
from.

We need a small, named set of building blocks so that the team has a
shared model of what an entity is, where invariants live, and how
write operations are structured.

## Decision drivers

- Push behavior into the model — controllers and handlers stay thin.
- Make invariants explicit and impossible to bypass.
- Keep the vocabulary small. We do not need every DDD concept on day
  one.
- Compatibility with EF Core — building blocks must be persistable
  without contortions.

## Considered options

- **Tactical DDD building blocks** (entities, value objects,
  aggregates, domain events, repositories per aggregate root,
  specifications when needed).
- **Anemic domain + rich application services.** Data classes plus
  service classes that contain the business logic.
- **Transaction script per use case.** No domain layer at all; each
  handler is a procedural script.

## Decision outcome

Chosen option: **Tactical DDD building blocks**, applied inside each
module's `Domain` folder. The vocabulary is:

- **Entity** — has identity, has behavior. Methods that change state
  live on the entity, not in services. Constructors enforce invariants.
- **Value object** — no identity, immutable, equality by value. Use
  for concepts like `Title`, `EmailAddress`, `DueDate`.
- **Aggregate** — a cluster of entities and value objects with one
  root. The root is the only entry point and enforces invariants
  across the cluster. Repositories are per aggregate root, not per
  table.
- **Domain event** — raised by aggregates when something
  business-meaningful happens. Dispatched by the application layer
  after the unit of work commits successfully.
- **Repository** — interface in `Domain`, implementation in
  `Infrastructure`. Exposes aggregate-shaped operations
  (`GetById`, `Add`, `Remove`), not generic `IQueryable`.
- **Specification** — used only for non-trivial queries that belong to
  the domain vocabulary. Do not introduce specifications for one-off
  predicates.

Concepts we do **not** adopt yet: domain services unless behavior
genuinely does not belong to a single aggregate; bounded-context maps
beyond what ADR-0002 already implies; saga/process manager (defer to
when sync work begins).

### Consequences

- Positive: the domain layer reads as a model of the business, not a
  set of structs.
- Positive: invariants are enforced in one place per aggregate.
- Positive: testing the domain does not require any framework.
- Negative: more types than an anemic model — small features can feel
  ceremonial. We mitigate this by allowing simple value-object-free
  entities until a second use case justifies a value object.
- Negative: EF Core mappings for value objects and private setters
  need explicit configuration; we accept that cost.

## Links

- Builds on: ADR-0002 (Clean Architecture + Modular Monolith).
- Related: ADR-0004 (CQRS handlers stay thin because behavior lives on
  the aggregate), ADR-0005 (EF Core mapping conventions for these
  building blocks).
