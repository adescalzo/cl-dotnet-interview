# 0007 - Use RFC 7807 ProblemDetails for HTTP error responses

- Status: accepted
- Date: 2026-04-19
- Deciders: TodoApi team

## Context and problem statement

Handlers return `Result` / `Result<T>` with a categorized `Error`
(ADR-0006). The `Api` layer needs a single, consistent way to render
those failures over HTTP so that clients (the React frontend, the
external integration tests, and any future consumer) can parse error
responses without learning a per-endpoint format.

Without a standard, each controller invents its own error JSON
(`{ "error": "..." }` here, `{ "message": "..." }` there, plain text
elsewhere). Validation errors in particular tend to grow ad-hoc
shapes.

## Decision drivers

- One error format across the entire API.
- A format that ASP.NET Core supports out of the box, so we are not
  fighting the framework.
- Machine-readable enough for clients to branch on (typed `type` URI
  and stable error `code`) and human-readable enough for logs.
- Compatible with the integration tests we have to satisfy
  (<https://github.com/crunchloop/interview-tests>) — those tests look
  at status codes and basic body shape, and `ProblemDetails` does not
  conflict with that.

## Considered options

- **RFC 7807 `ProblemDetails` / `ValidationProblemDetails`**, rendered
  with `application/problem+json`. ASP.NET Core has first-class
  support (`Results.Problem`, `IProblemDetailsService`,
  `AddProblemDetails`).
- **Custom error envelope** (`{ "error": { "code", "message", ... } }`).
- **Plain status code with empty body** for 4xx and 5xx.
- **GraphQL-style errors array** in a 200 response.

## Decision outcome

Chosen option: **RFC 7807 `ProblemDetails`**, with extensions for our
`Error` model.

Conventions:

- Enable the framework's problem details infrastructure in
  `Program.cs`:
  - `services.AddProblemDetails(...)` to customize defaults
    (`type` URIs, `instance` = request path, `traceId` extension).
  - `app.UseExceptionHandler()` and `app.UseStatusCodePages()` so that
    unhandled exceptions and bare status codes also render as
    `ProblemDetails`.
- A single `ResultExtensions.ToActionResult()` (or equivalent for
  Minimal APIs) maps `Result.Error.Category` to HTTP status:

  | `Error.Category` | HTTP status |
  |------------------|-------------|
  | `Validation`     | 400         |
  | `Unauthorized`   | 401         |
  | `Forbidden`      | 403         |
  | `NotFound`       | 404         |
  | `Conflict`       | 409         |
  | `Unexpected`     | 500         |

- Every `ProblemDetails` body carries:
  - `type` — a stable URI per error code, e.g.
    `https://api.todoapi/errors/todolists.not_found`. The URI does not
    have to resolve; it just has to be stable.
  - `title` — short human label (`"Todo list not found."`).
  - `status` — the HTTP status above.
  - `detail` — the `Error.Message`.
  - `instance` — the request path (set automatically).
  - Extension `code` — the `Error.Code` from ADR-0006
    (`"todolists.not_found"`).
  - Extension `traceId` — the current activity / correlation id.
- Validation errors use `ValidationProblemDetails` with the standard
  `errors` dictionary keyed by field name. Per-field codes go in an
  extension to preserve the typed `code` from ADR-0006.
- Controllers do **not** build `ProblemDetails` by hand. They call
  the shared mapping helper (`result.ToActionResult()` /
  `result.ToHttpResult()` for Minimal APIs) so the format stays
  uniform.
- The global exception handler renders unexpected exceptions as a
  500 `ProblemDetails` with a generic message — no stack traces or
  internal details in the response body. Stack traces go to the log,
  correlated by `traceId`.

### Consequences

- Positive: one error format for the whole API. Clients write one
  parser.
- Positive: validation and domain errors share a structure; clients
  inspect the extension `code` for branching.
- Positive: ASP.NET Core does the heavy lifting — minimal custom
  middleware.
- Positive: integration with observability is straightforward
  (`traceId` in the body matches the log entry).
- Negative: a tiny bit more JSON per error response than a bare
  status code. Acceptable.
- Negative: the `type` URI scheme has to be agreed once and kept
  stable. We treat it as part of the public API contract.

## Links

- RFC 7807: <https://datatracker.ietf.org/doc/html/rfc7807>
- ASP.NET Core problem details:
  <https://learn.microsoft.com/aspnet/core/fundamentals/error-handling#problem-details>
- Builds on: ADR-0006 (`Result` and `Error.Category` drive the
  mapping). Related: ADR-0004 (controllers translate handler results
  via the shared helper).
