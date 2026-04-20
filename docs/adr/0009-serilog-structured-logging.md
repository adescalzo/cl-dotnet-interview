# 0009 - Serilog for structured logging, with Console / File / Seq sinks

- Status: accepted
- Date: 2026-04-19
- Deciders: TodoApi team

## Context and problem statement

The starter project logs through the default
`Microsoft.Extensions.Logging` console provider wired in `Program.cs`:

```csharp
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
```

That is fine for a scratchpad and insufficient for what we want next.
We are about to introduce Wolverine (ADR-0008), correlate traces with
`ProblemDetails.traceId` (ADR-0007), and eventually run this API in
a devcontainer/CI where plain-text console output loses signal. We
need:

- Structured (JSON / key-value) log records, not interpolated strings.
- Multiple sinks: developer console, a rolling file, and a queryable
  dev-time backend (Seq) so the team can pivot on `traceId`, `code`,
  `module`, etc.
- A pipeline that works with Wolverine's `OpenTelemetry` support so
  one configuration covers both CQRS middleware and request logs.

## Decision drivers

- Structured logs with a typed pipeline (enrichers, filters) — built
  in MEL does not give us this without heavy config.
- Seq for dev-time ad-hoc querying; File for CI/run persistence;
  Console for the developer loop.
- The chosen library must play nicely with ASP.NET Core's logging
  abstraction (`ILogger<T>` stays the primary API).
- Compatibility with Wolverine's OTel exporters — we do not want
  parallel logging stacks.

## Considered options

- **Serilog** with `Serilog.AspNetCore` + Console / File / Seq sinks.
- **NLog** with similar sink ecosystem.
- **Built-in `Microsoft.Extensions.Logging`** + a custom JSON
  formatter + a third-party Seq provider.
- **OpenTelemetry-only** (logs routed purely through OTel exporters).

## Decision outcome

Chosen option: **Serilog**, installed via these packages:

- `Serilog.AspNetCore` — wires Serilog as the host logging provider
  and exposes `UseSerilog(...)`.
- `Serilog.Sinks.Console` — developer console output.
- `Serilog.Sinks.File` — rolling file sink (`logs/todoapi-.log`,
  daily roll).
- `Serilog.Sinks.Seq` — Seq sink for structured log search in dev.

Conventions:

- Configuration lives in `appsettings*.json` under a `Serilog` section,
  read via `.ReadFrom.Configuration(context.Configuration)`. Code in
  `Program.cs` only bootstraps the logger and hands it to Serilog's
  `UseSerilog`.
- Enrichers required on every log record: `FromLogContext`
  (propagates ambient properties such as `TraceId`, `Module`,
  `UserId`), `WithMachineName`, `WithEnvironmentName`. Optional per
  sink.
- The Seq sink is configured but the connection string defaults to
  `http://localhost:5341` for the devcontainer. Failing writes to Seq
  must not crash the app — Serilog's default behavior (silent
  failure with an internal trace) is acceptable.
- File sink rolls daily under `./logs/` with a 30-day retention cap.
- `ProblemDetails.traceId` (ADR-0007) is the same id that appears in
  logs — both come from `Activity.Current?.Id`, so an operator can
  pivot from an HTTP error back to the log entries that produced it.
- We do **not** log full request/response bodies by default — only
  route, status, elapsed ms, and `traceId`. Add per-endpoint
  request-body logging only with explicit scope and redaction.

### Consequences

- Positive: one logging library across the app, Wolverine
  middleware, and the global exception handler.
- Positive: structured output means `code`, `module`, and `traceId`
  can be queried directly in Seq without regex over strings.
- Positive: configuration-driven — sinks can be added/removed per
  environment without touching code.
- Positive: aligns with ADR-0007 (`traceId` appears both in the error
  body and the log record).
- Negative: one more library in the dependency graph. Accepted;
  Serilog is mature and widely used.
- Negative: Seq is an extra service to run in the devcontainer.
  Acceptable; it is not a deploy dependency, only a dev tool.

## Links

- Builds on: ADR-0002 (logging is cross-cutting, lives at composition
  root).
- Related: ADR-0007 (`traceId` correlation), ADR-0008 (Wolverine's
  OpenTelemetry hooks feed the same pipeline).
- Serilog: <https://serilog.net>.
- Seq: <https://datalust.co/seq>.
