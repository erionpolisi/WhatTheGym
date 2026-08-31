# Architecture

WhatTheGym is a single deployable monolith with one PostgreSQL database.

## Layers and dependency direction

```
Gym.Api  ──────────►  Gym.Application  ──────────►  Gym.Domain
   │                        ▲
   └──►  Gym.Infrastructure ┘        (Infrastructure implements Application ports)
```

- **Gym.Domain** — entities, enums, invariants, the score calculator, `Result`/`Error`.
  No dependencies at all.
- **Gym.Application** — commands, queries, handlers (`ICommandHandler`/`IQueryHandler`,
  CQRS-light without a mediator), FluentValidation validators, DTOs, and small
  purpose-built ports (`IGymRepository`, `IGymSearchQuery`, `ISearchIndex`,
  `IEmailOutbox`, `ISecureTokenService`, ...). No EF Core reference.
- **Gym.Infrastructure** — EF Core (`AppDbContext`), migrations, repository/port
  implementations, PostgreSQL full-text search, Resend email adapter, outbox
  processor, retention sweeper, seeding.
- **Gym.Api** — HTTP only: controllers (mapping + delegation), middleware
  (correlation id, ProblemDetails exception handler), cookie/OIDC auth
  composition, rate limiting, CORS, Swagger, health checks, DI wiring.

## Cross-cutting behaviour

- **Result pattern** for expected errors; `ErrorType` maps deterministically to
  HTTP status codes (400/401/403/404/409/429/500) as RFC 7807 ProblemDetails
  with a stable `code` extension and `correlationId`.
- **Serilog** structured logging; every request carries `X-Correlation-Id`
  (accepted from the caller or generated) via `CorrelationIdMiddleware`.
- **API versioning**: all routes live under `/api/v1`.
- **Health**: `/health/live` (self) and `/health/ready` (database connectivity).
- **Rate limiting**: fixed-window in-memory limiter partitioned by client IP on
  public write endpoints (`public-write`, `auth`, `analytics` policies). IPs are
  used only in memory, never persisted.
- **Materialized scores**: `GymRatingSummary` is recomputed inside the same unit
  of work whenever a score-relevant review changes; an admin rebuild command
  recomputes everything.
- **Background services**: transactional email outbox processor (retry with
  exponential backoff) and daily retention sweeper (respects legal holds).

## Search

PostgreSQL-only, behind the `ISearchIndex`/`IGymSearchQuery` ports:

- database-generated `tsvector` column (`german` config) over gym name and
  address with a GIN index,
- `pg_trgm` GIN index over the name for fuzzy matching,
- chain name ILIKE fallback,
- ranking by `ts_rank + similarity` when a term is present.

The write-side `ISearchIndex` is a no-op for PostgreSQL (the column is
database-generated) but keeps the seam for a future external index.

## Authentication (BFF)

Google-only login. The API is the backend-for-frontend: it runs the
Authorization Code Flow with PKCE against Google, then issues its own session
cookie plus a rotating refresh token (stored hashed server-side, reuse
detection revokes the whole family). Provider tokens are never persisted and
never reach the browser. Locally, a Development-only dev-login replaces Google.
See [adr/0003-authentication.md](adr/0003-authentication.md).

Hardening (see [adr/0012-security-hardening.md](adr/0012-security-hardening.md)):

- **Per-request session revalidation**: `OnValidatePrincipal` checks every
  cookie session against the user store, so role changes, account deletion and
  server-side revocation take effect immediately (stale claims are replaced,
  inactive users rejected).
- **CSRF defense in depth**: authenticated state-changing requests must carry
  `X-CSRF: 1` or a JSON content type (`CsrfHeaderMiddleware`); primary defense
  remains `SameSite=Lax` cookies, which requires frontend and API to be
  deployed same-site (e.g. `whatthegym.at` + `api.whatthegym.at`).
- **Forwarded headers**: behind a TLS-terminating ingress,
  `ForwardedHeaders:Enabled=true` makes the app honor `X-Forwarded-For/Proto`
  (client-IP rate limiting, correct OIDC redirect URIs). Only enable where the
  app is reachable exclusively through the ingress.
- **DB-enforced review uniqueness**: a filtered unique index guarantees one
  active review per user and gym; races surface as HTTP 409
  (`UniqueConstraintViolationException`).

## Composition

`Program.cs` wires everything: options binding, `AddApplication()` (reflection
scan of handlers/validators), `AddInfrastructure()` (DbContext, repositories,
adapters, hosted services), auth schemes, policies (`Moderator`, `Admin`),
rate limiter, CORS allowlist, Swagger, health checks, startup
migration/seeding (config-driven).
