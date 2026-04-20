# 0011 - Unit testing conventions for handlers and validators

- Status: accepted
- Date: 2026-04-20
- Deciders: TodoApi team

## Context and problem statement

ADR-0004/ADR-0008 put each use case behind a Wolverine handler, and
ADR-0003 pushes behavior onto aggregates. That makes handlers and
FluentValidation validators the two main seams we actually need to
cover with unit tests. Without a convention those tests drift into
three failure modes:

- Hand-rolled object construction in every `[Fact]`, so a new required
  field forces a sweep across dozens of tests.
- Mocks of `DbContext` / repositories that pass green while the EF
  mapping silently breaks.
- Ad-hoc naming and assertion styles that make the suite hard to scan.

We have a reference implementation we trust
(`challenges/tdh/src/TD.CodingExercise.WebApi.UnitTests`) that solves
this with three building blocks: **Bogus** for default data,
**immutable Builders** per test subject, and an **`AsyncLifetimeBase`**
that spins up an EF Core `InMemory` `DbContext` per test. We want to
adopt the same approach here, adapted to our stack (Wolverine + module
boundaries + `Result<T>`).

## Decision drivers

- One obvious way to build a valid `TodoList` / `TodoItem` / command
  payload in a test, so adding a required field is a one-line change.
- Tests run against a real `DbContext` with `InMemory` so EF mapping,
  navigations, and LINQ translation are exercised — no mocked
  repositories for handler tests.
- Validators are pure, so they get the simpler path: builders + FV's
  `TestValidate()`, no EF, no base class.
- Consistent naming and assertions across the suite — `FluentAssertions`
  reads closer to the domain vocabulary than `Assert.Equal`.
- Deterministic enough that CI is not flaky; random enough that tests
  do not accidentally couple to magic values.

## Considered options

- **Bogus + immutable Builders + `AsyncLifetimeBase` over EF InMemory
  (tdh style).** Handler tests hit a real `DbContext`. Validator tests
  stay pure.
- **Unit-only with mocked repositories (NSubstitute everywhere).** Fast
  and isolated, but any EF-level bug (missing `Include`, bad
  configuration, value-object mapping) passes green.
- **AutoFixture / AutoData.** Zero-maintenance random data, but the
  resulting tests are hard to read ("where does this value come from?")
  and overrides are awkward once aggregates have invariants.
- **Hand-rolled arrange blocks, no builders.** Zero infrastructure,
  maximum churn when the model changes.

## Decision outcome

Chosen option: **Bogus + immutable Builders + `AsyncLifetimeBase` over
EF InMemory (tdh style)**, with validators exempted from the base
class.

### Libraries

Added to `Directory.Packages.props`, referenced only from the test
project(s):

- `Bogus` — data generation inside builders.
- `FluentAssertions` — assertion style across all tests.
- `NSubstitute` — mocking framework for the few collaborators that
  genuinely need to be faked (clock, outbound ports). Not used for the
  `DbContext` or repositories in handler tests.
- `Microsoft.EntityFrameworkCore.InMemory` — already in the test
  project (ADR-0005).

### Test project layout

The test project mirrors the production tree:

```
TodoApi.Tests/
  Application/
    Commands/<UseCase>/<UseCase>HandlerTests.cs
    Commands/<UseCase>/<UseCase>ValidatorTests.cs
    Queries/<UseCase>/<UseCase>HandlerTests.cs
  Domain/
    <Aggregate>Tests.cs            # invariants, state transitions
  Infrastructure/
    Persistence/...                # EF configuration tests if needed
  Builders/
    <Aggregate>Builder.cs
    <Command>PayloadBuilder.cs
  TestSupport/
    AsyncLifetimeBase.cs
    IBuilder.cs
```

### Builders

Every test subject (aggregate, child entity, value object, command,
query, DTO) that appears in more than one test gets a builder. Rules:

- Implement `IBuilder<T>`:

  ```csharp
  public interface IBuilder<out T> where T : class
  {
      T Build();
      IEnumerable<T> BuildList(int count);
  }
  ```

- Defaults come from a private nested `*Data` POCO populated by a
  `Faker<TData>` with `RuleFor(...)` — one rule per field. Defaults
  must produce a **valid** aggregate / payload out of the box.
- Overrides are **fluent and immutable**: each `With*` returns a new
  builder wrapping `_faker.Clone().RuleFor(...)`. No mutable state, no
  cached `Build()` result — every call regenerates.
- `Build()` maps the `*Data` POCO onto the real type through its
  public constructor / factory so invariants run. Do not reach into
  private state from builders.
- No cross-builder composition by default. If a test needs a
  `TodoList` with three items, it calls `TodoListBuilder` and
  `TodoItemBuilder` itself — explicit wins over implicit.
- No global seed. Tests must not depend on specific generated values;
  assert on the value they put in via `With*`, not on what Bogus
  produced.

### AsyncLifetimeBase (handler tests only)

A shared abstract base in `TestSupport/` that implements xUnit's
`IAsyncLifetime` and provides a fresh, isolated EF Core `InMemory`
`DbContext` per test:

- Database name is `"<Module>_Tests_{Guid.NewGuid()}"` so tests do not
  leak state into each other.
- Exposes a `protected DbContext Context { get; }` and a
  `protected Task SaveChangesAsync()` helper.
- Exposes NSubstitute-based defaults for genuine ports (`IClock`,
  outbound services). Handler tests override only what they need.
- Virtual hooks:
  - `OnInitializeAsync()` — test-class-specific setup (e.g.
    `await Context.Database.EnsureCreatedAsync()`, seeding, handler
    construction).
  - `OnDisposeAsync()` — teardown (e.g.
    `await Context.Database.EnsureDeletedAsync()`).
- **Not** used with `[Collection]` / `ICollectionFixture` — each test
  class inherits directly; xUnit handles lifetime per test method.

Handler tests construct the handler with the real repository +
`DbContext`. Repositories are **not** mocked; that is the whole point
of running through InMemory.

### Validator tests (no base class)

Validators are pure, do not touch persistence, and do not need
lifetime management. They use:

- FluentValidation's `TestValidate()` / `ShouldHaveValidationErrorFor`
  / `ShouldNotHaveValidationErrorFor`.
- Builders for the command/query payload under test.
- `[Theory]` + `[InlineData]` for range / boundary cases; `[Fact]` for
  one-off scenarios.

### Naming and structure

- One test class per production class:
  `<ClassUnderTest>Tests` — e.g. `CreateTodoListHandlerTests`,
  `CreateTodoListCommandValidatorTests`.
- Method naming: `Method_WhenCondition_ShouldOutcome`
  (e.g. `Handle_WhenTitleIsBlank_ShouldReturnValidationError`).
- Arrange / Act / Assert comments in every test body. Assertions use
  FluentAssertions (`.Should().Be(...)`, `.Should().BeOfType<...>()`,
  etc.) — no `Assert.Equal` in new tests.
- Handler tests assert on the returned `Result<T>` first (success or
  `Error.Category`), then re-read from `Context` with `AsNoTracking`
  to verify the persisted state.

### Consequences

- Positive: adding a required field to an aggregate or command means
  one edit in the builder, not a sweep.
- Positive: handler tests catch EF configuration regressions that
  mock-based tests silently pass.
- Positive: validator tests stay fast and focused — no lifecycle
  machinery.
- Positive: a single naming / assertion style makes the suite
  scannable.
- Negative: builders and `AsyncLifetimeBase` are test infrastructure
  to maintain. We accept that cost; tdh has shown it pays off past
  the second test class.
- Negative: EF Core `InMemory` is not SQL Server — it does not catch
  every real-database behavior (e.g. concurrency tokens, case
  sensitivity). Integration tests at the HTTP level
  (`crunchloop/interview-tests`) and any future SQL-backed tests
  cover that gap.

## Links

- Builds on: ADR-0003 (DDD building blocks — aggregates own behavior,
  so that is what we test), ADR-0005 (EF Core; `InMemory` is
  already approved for unit tests), ADR-0006 (handlers return
  `Result<T>`; tests assert on `Error.Category`), ADR-0008 (Wolverine
  handlers as the unit under test).
- Reference implementation:
  `challenges/tdh/src/TD.CodingExercise.WebApi.UnitTests`.
