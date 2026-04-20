# 0001 - Record architecture decisions

- Status: accepted
- Date: 2026-04-19
- Deciders: TodoApi team

## Context and problem statement

We are reshaping the TodoApi codebase from the interview starter
template into a Clean Architecture + Modular Monolith with DDD and
CQRS. Decisions of that size — choosing layers, libraries,
persistence strategy, error-handling style — need to be visible and
durable so that future contributors (and future Claude sessions) can
understand why the code is the way it is, and so that we can revisit
or overturn a choice without re-discovering it from scratch.

Without a written record, the reasoning behind a decision lives only
in chat history and the heads of whoever was in the room. That makes
the codebase harder to maintain, harder to onboard onto, and easier
to drift away from on autopilot.

## Decision drivers

- Long-lived clarity about *why* the architecture looks the way it does.
- Cheap process — recording a decision must not slow the team down.
- Compatibility with how Claude Code sessions read context (plain
  Markdown in the repo, easy to grep).
- A way to overturn an old decision without rewriting history.

## Considered options

- **MADR (Markdown ADR), short form.** Lightweight Markdown template,
  numbered files, status field, supersession by reference.
- **Nygard ADR, original form.** Same idea, slightly looser template.
- **No formal record; rely on commit messages + PR descriptions.**
- **Confluence / external doc tool.**

## Decision outcome

Chosen option: **MADR short form**, stored in `docs/adr/` inside the
repo, numbered sequentially starting at `0001`.

### Consequences

- Positive: decisions live next to the code they affect, are diffable,
  and can be reviewed in PRs alongside the implementation.
- Positive: numbered, never-renumbered files give a stable reference
  scheme (`ADR-0004`) that other ADRs and code comments can point at.
- Positive: superseded ADRs stay in the repo with an updated status
  field, preserving the trail.
- Negative: writers have to remember to add an ADR. CLAUDE.md lists
  the trigger conditions to make this explicit.
- Neutral: ADRs are not a substitute for code documentation or for PR
  descriptions; they record *decisions*, not *changes*.

## Links

- Template and conventions: see `CLAUDE.md`, section
  "Architecture Decision Records (ADRs)".
- MADR project: <https://adr.github.io/madr/>
