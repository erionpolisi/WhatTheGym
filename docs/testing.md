# Testing strategy

How WhatTheGym is tested, what each layer owns, and how to run everything.
Counts last updated 2026-08-31 (Result3 test campaign).

## Layers and ownership

| Layer | Project | Owns | Count |
| --- | --- | --- | --- |
| Domain unit | `Gym.Domain.Tests` | entity invariants, state machines, score aggregation math, sanitization, Result primitives | 535 |
| Application unit | `Gym.Application.Tests` | every FluentValidation validator boundary, handler behavior over fake ports, error mapping, token hashing, mail enqueueing | 420 |
| Integration | `Gym.IntegrationTests` | real HTTP against the real API + Testcontainers PostgreSQL: contracts, auth, search SQL, legal flow, privacy, rate limits, seeding regressions | 299 |
| Manual/exploratory | `backend/http/` | admin operations, environment smoke, error-path probes | ~80 requests |
| Static/security | CI: analyzers (warnings-as-errors), CodeQL, Trivy, Dependabot | code + dependency hygiene | — |

Layering rules the tests encode:

- Entity factories guard **domain invariants** (rating 1–5, text length after
  sanitization, legal state transitions). **Format** validation (email syntax,
  enum strings, pagination bounds) is owned by API-level validators — domain
  tests document this split explicitly.
- Missing rating data is `null`, never `0`; every score test asserts this.
- Rounding is `MidpointRounding.AwayFromZero` at exactly 2 decimals, tested at
  midpoints.
- Every bug found manually gets a failing test before the fix (see
  `DemoSeedRegressionTests` for the template).

## How to run

```bash
cd backend
dotnet test tests/Gym.Domain.Tests          # fast, no dependencies
dotnet test tests/Gym.Application.Tests     # fast, fakes only
dotnet test tests/Gym.IntegrationTests      # needs Docker (Testcontainers)
dotnet test                                 # everything
```

Integration tests boot disposable PostgreSQL containers; the shared
`WtgApiFactory` (collection fixture "api") seeds the catalogue WITHOUT demo
data. `DemoSeedApiFactory` exists for regressions that only reproduce with
development demo data.

## Edge-case map (what the campaign deep-dived)

- **Scoring**: single-category, area-only bases, 50/50 exactness, midpoint
  rounding, large inputs, per-category counts, missing-is-null guarantees.
- **Reviews**: all 11 categories × range bounds, text 4000/4001 (after
  sanitizer), every deletion origin, delete/restore, legal hide/release/
  removal/reinstate transitions incl. the soft-deleted→UnderReview guard
  (regression: fast-track must not resurrect author-deleted reviews).
- **Legal cases**: full state machine incl. illegal transitions, appeal
  deadline boundaries, token hashing, append-only event sequences.
- **Validators**: null/empty/whitespace, min−1/min/max/max+1, invalid enums,
  email sets, district 0/1/23/24, page bounds, German message spot checks.
- **API contracts**: auth rotation/reuse, role boundaries (401/403 matrices),
  filter/pagination edges, ETags, honeypots, rate limits, seeded-data
  regressions.

## Deliberately not added (rationale in TASKS.md)

Playwright/Cypress E2E, k6 load tests, mutation testing, frontend unit tests —
revisit post-launch when the UI stabilizes and real traffic patterns exist.
