# WhatTheGym Agent Instructions

## Authority and Scope

- Treat [CONSTRAINTS.md](CONSTRAINTS.md) as the authoritative product decision
  record. [Prompt.md](Prompt.md) is the implementation handoff.
- Build a production-ready MVP, not a throwaway prototype. Keep changes small,
  testable, and consistent with the specified architecture.
- User-facing text is German (`de-AT`); code, comments, documentation
  structure, and commit messages are English.
- Do not add mobile apps, payments, studio dashboards, affiliate features,
  AI features, image uploads, object storage, microservices, message brokers,
  Kubernetes, PostGIS, geo search, or external search services to the MVP.

## Runtime and Delivery Order

- Make the complete application run locally first through Docker Compose:
  API, normal PostgreSQL, and optional pgAdmin. No cloud account is required
  for local development or tests.
- Implement `backend/`, `frontend/`, `infrastructure/`, and `docs/` in the
  monorepo. The backend is the first priority; frontend integration follows.
- Prepare Azure Bicep and deployment documentation for local, staging, and
  production, but do not deploy or require Azure credentials yet.
- Target the lowest viable ongoing Azure cost, capped at 10 EUR/month. Record
  cost tradeoffs in an ADR. Until a registry is configured, CI publish only
  builds/verifies artifacts and images; it must not push or deploy.

## Architecture

- Use .NET 8, ASP.NET Core Web API, EF Core, PostgreSQL, REST, Swagger/OpenAPI,
  Clean Architecture, CQRS-light, Result pattern, FluentValidation, Serilog,
  RFC 7807 ProblemDetails, API versioning at `/api/v1`, health checks, and
  nullable reference types with warnings treated as errors.
- Preserve dependency direction: Domain has no dependencies; Application owns
  use cases and ports; Infrastructure owns EF Core and external adapters; API
  owns HTTP, middleware, and composition.
- Keep controllers limited to mapping and delegation. Use small, purpose-built
  interfaces. Do not instantiate infrastructure inside application use cases.
- Add migrations, deterministic development-only seed data, unit tests, and
  PostgreSQL-backed integration tests. Run focused validation after each edit.
- CI gates are: `restore -> build -> unit tests -> integration tests ->
  migration/schema check -> security scan -> publish`. Use normal PostgreSQL,
  Dependabot, CodeQL, and Trivy; high/critical findings block the build.

## Authentication and Roles

- Authentication is Google-only via ASP.NET Core BFF, Authorization Code Flow
  with PKCE, and `Secure`/`HttpOnly`/`SameSite` cookies. Never expose provider
  or application tokens to Next.js, `localStorage`, or `sessionStorage`.
- Enforce the documented local, staging, and production CORS allowlists in the
  README. Store rotating refresh tokens hashed server-side and revoke them on
  logout or reuse detection.
- Roles are `User`, `Moderator`, and `Admin`. Admins manage roles and all
  resources; moderators may remove reviews. Bootstrap the first admin only
  through a configured verified Google email, never through seed data.

## Review and Score Rules

- Reviews are never anonymous. A Google account with `email_verified = true`
  receives the "Verifiziert ueber Google" badge; it is not proof of gym
  membership.
- A review needs at least one direct 1-5 rating. All four membership and all
  seven studio categories are direct ratings; free text and further categories
  are optional.
- Publish reviews automatically. Moderate only reports or content flagged by
  the basic server-side anti-abuse filter.
- Review deletion is reversible soft delete. Deleted or under-review content
  is not public and never contributes to score summaries, but remains archived
  under retention/legal-hold rules.
- Aggregate each area from available category data. Compute total score as
  50/50 between membership and studio only when both exist; otherwise use the
  available area and expose `scoreBasis`. Never treat missing data as zero.
- Do not store, display, filter, or rank by membership prices. Support only
  district, total/area score, chain, and branch search/filtering. No location-
  based search in this MVP.

## Reporting, Legal, Privacy, and Mail

- There is exactly one public review-report flow, creating a `LegalCase`.
  Do not introduce `ReviewReport`, partial redactions, or `ReviewRedaction`.
- A LegalCase decision is `KeepOnline` or `FullyRemoved`; the latter changes
  the review to `RemovedLegal`. Keep reported content online until a documented
  decision, except an explicitly classified obviously-illegal fast-track case.
- Legal audit events are append-only and immutable, with configurable
  retention, legal holds, and a seven-year default after case closure. Appeals
  must remain available at least six months after the original decision.
- Treat every value logically linkable to a person as personal data. Map and
  test it in the generated processing-activities record. Never expose email,
  sensitive case data, tokens, or archived review data publicly.
- Account deletion anonymizes/restricts data according to configured retention
  and legal holds; only Admins access retained open-case data.
- Persist email work in an outbox before sending through Resend. Retry failures
  safely and record the exact legal notice text in the case audit trail.
- Use only rate limiting, honeypots, and server-side spam/abuse checks for MVP
  public forms; do not add CAPTCHA or external anti-abuse vendors.

## Data and Frontend

- Only Admins create studios directly. Public additions/corrections are
  `ContactRequest` submissions reviewed by an Admin.
- Opening hours are optional and entered from official data; do not integrate
  Google Maps. Demo reviews and LegalCases exist only in local/development,
  never staging or production.
- Build the required Next.js frontend as a thin, SEO-aware consumer of the
  backend. Swagger is sufficient for administration; do not build an admin UI
  for the MVP.

## Documentation and Decisions

- Document meaningful implementation choices in `docs/adr/`, especially
  security, retention, cost, and future PostGIS decisions. Mark legal copy
  "ENTWURF - anwaltlich pruefen lassen".
- Do not silently change the product rules above. When a new requirement is
  ambiguous, choose a conservative documented ADR default unless it is
  irreversible or legal/security-critical; then ask the user.