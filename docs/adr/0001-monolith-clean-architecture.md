# ADR 0001: Monolith with Clean Architecture and CQRS-light

Status: accepted (mandated by CONSTRAINTS.md) — 2026-08-21

## Context

The MVP must be a production-ready, locally runnable review platform with a
hard cost cap and a small team. CONSTRAINTS.md prescribes a single deployable
monolith, one PostgreSQL database, and Clean Architecture layering.

## Decision

Four projects with strict dependency direction (`Domain` ← `Application` ←
`Infrastructure`/`Api`). CQRS-light is implemented as plain
`ICommandHandler`/`IQueryHandler` interfaces with constructor-injected,
purpose-built ports — no MediatR, no event bus, no reflection dispatcher at
call sites. Expected errors use a `Result`/`Error` type mapped centrally to
RFC 7807 ProblemDetails.

## Consequences

- Simple mental model, trivial local debugging, one deployment unit.
- Handlers are individually testable with in-memory fakes; controllers stay
  mapping-and-delegation only.
- No distributed-system machinery to maintain; future extraction remains
  possible along the port seams.
