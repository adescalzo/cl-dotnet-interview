# 0005 - Entity Framework Core for persistence, one DbContext per module

- Status: accepted
- Date: 2026-04-19
- Deciders: TodoApi team

## Context and problem statement

Each module owns its aggregates (ADR-0003) and exposes repositories as
interfaces in its `Domain` layer. We need a concrete persistence
technology in `Infrastructure`, plus rules about how `DbContext`s and
migrations are organized so that modules stay independent.

The starter project already uses EF Core with a single `TodoContext`
serving the whole API. That single-context shape will block the
modular monolith goal: as modules are added, every team will want to
edit the same `DbContext`, and migrations will become a coordination
point.

## Decision drivers

- Aggregate boundaries (ADR-0003) must drive persistence boundaries —
  one module should not be able to load another module's aggregate by
  reaching across a shared context.
- The devcontainer already provisions SQL Server. Tests already use
  EF Core's `InMemory` provider. We do not want to fight that.
- Migrations should be ownership-clear: a module's migration belongs
  to that module.
- `Directory.Packages.props` already pins `Microsoft.EntityFrameworkCore`
  at version 10.0.1 with `Relational`, `Design`, and `InMemory`
  providers — the team has implicitly committed to EF Core.

## Considered options

- **EF Core with one `DbContext` per module.**
- **EF Core with a single shared `DbContext`** (current shape).
- **Dapper / raw ADO.NET.** Hand-written SQL per repository.
- **A document store** (Marten, Mongo).

## Decision outcome

Chosen option: **EF Core, with one `DbContext` per module**.

Conventions:

- Each module's `DbContext` lives in its `Infrastructure/Persistence/`
  folder and is named for the module (e.g. `TodoListsDbContext`).
- Mapping uses `IEntityTypeConfiguration<T>` files under
  `Infrastructure/Persistence/Configurations/`.
  `OnModelCreating` only calls
  `modelBuilder.ApplyConfigurationsFromAssembly(...)`.
- Aggregates are mapped explicitly: value objects via
  `OwnsOne`/`OwnsMany` or value converters; private setters where
  invariants demand it; backing fields for collections.
- Repository implementations live next to the `DbContext`, expose
  aggregate-shaped APIs, and **do not** leak `IQueryable` outside
  `Infrastructure`. Specifications (ADR-0003) translate to
  `IQueryable` inside the repository.
- Migrations belong to the module that owns the `DbContext`. Run with
  `dotnet ef migrations add <Name> --project TodoApi.Modules.<Name>`
  (or whichever project hosts the `DbContext`).
- Tests use the `InMemory` provider with a per-test database name
  (`Guid.NewGuid().ToString()`). Tests that depend on relational
  semantics (transactions, raw SQL, concurrency tokens) target SQL
  Server in the devcontainer instead.
- The unit-of-work boundary is the `DbContext.SaveChangesAsync` call
  inside the handler / pipeline behavior. Domain events are dispatched
  *after* `SaveChangesAsync` returns successfully (ADR-0003).

### Consequences

- Positive: each module's persistence concerns are isolated.
  Refactoring or extracting a module later does not require unwinding
  a shared schema.
- Positive: migrations are owned per module — no central bottleneck.
- Positive: the existing test pattern (`InMemory`, controller-level)
  ports forward to handler-level tests with minimal change.
- Negative: cross-module reads cannot just `JOIN` across contexts.
  This is intentional — cross-module data flows go through application
  contracts (ADR-0002).
- Negative: aggregates with rich value-object mappings need explicit
  EF Core configuration; conventions alone will not cover them.
- Negative: each `DbContext` carries its own migrations history table.
  Acceptable given the modular goal.

## Links

- Builds on: ADR-0002 (modules), ADR-0003 (aggregate-shaped repositories).
- Related: ADR-0004 (handlers commit the unit of work), ADR-0006
  (repository methods return `Result` for not-found rather than null
  where appropriate).
