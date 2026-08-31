# WhatTheGym

Fitnessstudio-Bewertungsplattform fuer Wien. Production-ready monolithic MVP:
.NET 8 Clean Architecture API + PostgreSQL + Next.js frontend, fully runnable
locally via Docker Compose — no cloud accounts or secrets required.

User-facing content is German (`de-AT`); code and documentation are English.

## Quickstart (local, Docker Compose)

Prerequisites: Docker Desktop.

```bash
cp .env.example .env       # defaults work out of the box
docker compose up --build
```

- API + Swagger (admin interface): http://localhost:7001/swagger
- Health: http://localhost:7001/health/live and /health/ready
- Optional pgAdmin: `docker compose --profile tools up` → http://localhost:5050

Migrations apply automatically and the official Vienna catalogue (~50 studios),
legal document drafts, and demo reviews/cases are seeded (demo data only in the
Development environment — never staging/production).

Frontend:

```bash
cd frontend
cp .env.local.example .env.local
npm install
npm run dev                # http://localhost:3000
```

Log in locally via the dev login (no Google credentials needed): on
http://localhost:3000/konto open "Lokaler Dev-Login". Use
`admin@example.invalid` to become the bootstrap Admin (configurable via
`BOOTSTRAP_ADMIN_EMAIL`).

## Running the backend without Docker

Requires .NET 8 SDK and a running PostgreSQL (e.g. `docker compose up db`).

```bash
cd backend
dotnet build
dotnet ef database update --project src/Gym.Infrastructure --startup-project src/Gym.Api
dotnet run --project src/Gym.Api    # https://localhost:7001 + http://localhost:5001
```

## Tests

```bash
cd backend
dotnet test tests/Gym.Domain.Tests
dotnet test tests/Gym.Application.Tests
dotnet test tests/Gym.IntegrationTests   # requires Docker (Testcontainers PostgreSQL)
```

## Environments and CORS allowlist

| Environment | Frontend                      | API                               | Allowed CORS origin           |
| ----------- | ----------------------------- | --------------------------------- | ----------------------------- |
| Local       | http://localhost:3000         | https://localhost:7001            | http://localhost:3000         |
| Staging     | https://staging.whatthegym.at | https://api-staging.whatthegym.at | https://staging.whatthegym.at |
| Production  | https://whatthegym.at         | https://api.whatthegym.at         | https://whatthegym.at         |

The allowlist is configuration (`Cors:AllowedOrigins`); cookies are `HttpOnly`,
`SameSite=Lax` and `Secure` on HTTPS. Tokens never reach browser storage.

## Repository layout

```
backend/          .NET 8 solution (Domain, Application, Infrastructure, Api + 3 test projects)
frontend/         Next.js (App Router) SEO-aware thin client
infrastructure/   Azure Bicep (prepared, not deployed)
docs/             Architecture, domain, API, scoring, legal, ADRs, onboarding
```

## Key documentation

- [TASKS.md](TASKS.md) — road to launch: local finish, Azure cost plan, social
  validation, go-live checklist, monetization
- [docs/onboarding.md](docs/onboarding.md) — start here
- [docs/architecture.md](docs/architecture.md) — layers, ports, composition
- [docs/scoring.md](docs/scoring.md) — 50/50 aggregation and `scoreBasis`
- [docs/legal.md](docs/legal.md) — reports, LegalCase lifecycle, retention
- [docs/api.md](docs/api.md) — endpoint overview (Swagger is authoritative)
- [backend/http/](backend/http/README.md) — Rider HTTP client suite for manual
  testing and administration (all endpoints, all environments)
- [docs/deployment-azure.md](docs/deployment-azure.md) — prepared Azure setup
- [docs/adr/](docs/adr/) — architecture decision records

## Conventions

- Conventional Commits (`feat:`, `fix:`, `docs:`, ...), `main` + `feature/*` branches.
- CI gates: restore → build → unit tests → integration tests → migration/schema
  check → security scan (CodeQL, Trivy, Dependabot) → publish (build/verify
  only; no registry push until one is deliberately configured).
- All legal copy is draft: `ENTWURF - anwaltlich pruefen lassen`.
