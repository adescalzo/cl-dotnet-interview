# 0006 - Use Result<T> for expected failures, exceptions only for the truly exceptional

- Status: accepted
- Date: 2026-04-19
- Deciders: TodoApi team

## Context and problem statement

CQRS handlers (ADR-0004) need a way to report outcomes that are not
"happy path." Most outcomes are not bugs — validation rejections,
not-found, conflict, forbidden. If those are signaled by throwing
exceptions, the API ends up using exceptions for control flow,
controllers grow `try`/`catch` ladders, and the cost of every
not-found becomes a stack-walk.

We also need a stable, typed handoff between the `Application` layer
and the `Api` layer so that controllers can render the failure
consistently as `ProblemDetails` (ADR-0007) without inspecting
exception types.

## Decision drivers

- Expected failures are part of the normal flow and should be visible
  in the handler signature.
- Exceptions remain available for genuinely exceptional conditions
  (programmer errors, infrastructure failures we cannot recover from).
- Controllers should map application outcomes to HTTP without business
  logic.
- The shape must be friendly to pipeline behaviors that wrap handlers
  (logging, transactions): they need to inspect outcome without
  catching exceptions.

## Considered options

- **`Result` / `Result<T>` returned from every handler**, with a
  small set of `Error` categories (`Validation`, `NotFound`,
  `Conflict`, `Forbidden`, `Unexpected`).
- **Exceptions for expected failures.** Throw `NotFoundException`,
  `ValidationException`, etc. and translate at a global exception
  handler.
- **Discriminated union per use case.** Each handler returns its own
  union of success/failure cases.
- **Tuple return** `(T? value, Error? error)`.

## Decision outcome

Chosen option: **`Result` / `Result<T>` with a small `Error` model**.

Conventions:

- `Result` and `Result<T>` live in the shared kernel
  (`TodoApi.SharedKernel`), along with `Error`.
- `Error` is a value object with a stable `Code` (string) and a
  human-readable `Message`. Codes are namespaced per module
  (`todolists.not_found`, `todolists.title.empty`).
- `Error` carries a category enum:
  `Validation`, `NotFound`, `Conflict`, `Forbidden`, `Unauthorized`,
  `Unexpected`. Categories drive the HTTP mapping (ADR-0007); codes
  drive client-side handling.
- Handlers return `Result` for void use cases, `Result<T>` otherwise.
  They never throw for expected failures.
- Repositories return `Result<T>` for reads where "not found" is a
  normal outcome, or `T?` where null is genuinely the right shape
  (rare). Pick one per repository method and stick to it.
- Validators (FluentValidation-style or hand-rolled) produce a
  `Validation`-category error containing one or more field errors.
- Exceptions are reserved for: programmer errors (null where it must
  not be, broken invariant detected late), infrastructure failures
  (database unreachable, serialization broken), and anything the API
  cannot translate into a meaningful response. A global exception
  handler renders those as `500` `ProblemDetails`.

### Consequences

- Positive: handler signatures advertise their failure modes.
- Positive: pipeline behaviors can branch on `result.IsFailure`
  without catching exceptions.
- Positive: not-found and validation are cheap — no stack walk.
- Positive: feeds directly into the `ProblemDetails` mapping
  (ADR-0007): one switch per `Error.Category`.
- Negative: every consumer of a handler has to handle the `Result`
  explicitly. We accept this — silent failure is worse.
- Negative: a small `Result` type has to be hand-rolled and kept
  stable. We do not pull a heavyweight library for it.

## Links

- Related: ADR-0004 (handlers return `Result`), ADR-0007 (HTTP
  mapping of `Error.Category` to `ProblemDetails`).
