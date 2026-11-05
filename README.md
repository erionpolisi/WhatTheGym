# WhatTheGym

Fitnessstudio-Bewertungsplattform fuer Wien. Production-ready monolithic MVP:
.NET 8 Clean Architecture API + PostgreSQL + Next.js frontend, fully runnable
locally via Docker Compose. Public browsing needs no cloud credentials; login
uses the same Google OAuth client as staging.

User-facing content is German (`de-AT`); code and documentation are English.

## Quickstart (local, Docker Compose)

Prerequisites: Docker Desktop.

```bash
cp .env.example .env       # add GOOGLE_CLIENT_SECRET for login
docker compose up --build
```

- API + Swagger (admin interface): http://localhost:7001/swagger
- Website: http://localhost:3000
- Health: http://localhost:7001/health/live and /health/ready
- Optional pgAdmin: `docker compose --profile tools up` → http://localhost:5050

Migrations apply automatically and seed the official Vienna catalogue
(113 studios with inherited training-type and target-audience facets) and legal
document drafts. Demo reviews are disabled by default.

For frontend-only development with hot reload:

```bash
cd frontend
cp .env.local.example .env.local
npm install
npm run dev                # http://localhost:3000
```

For login, add `http://localhost:7001/api/v1/auth/google/callback` to the
staging OAuth client's authorized redirect URIs, put its secret in the ignored
`.env`, and set `BOOTSTRAP_ADMIN_EMAIL` to your verified Google email. Then use
"Mit Google anmelden" on http://localhost:3000/konto.

Transactional mail uses the persistent outbox and Resend when
`RESEND_API_KEY` is set; otherwise local mail is logged. Domain verification,
key isolation, and the delivery smoke test are in
[docs/onboarding.md](docs/onboarding.md#resend-setup).

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
infrastructure/   Azure Bicep and staging/production deployment tooling
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
- [docs/testing.md](docs/testing.md) — test strategy, layer ownership, edge-case map
- [backend/http/](backend/http/README.md) — Rider HTTP client suite for manual
  testing and administration (all endpoints, all environments)
- [docs/deployment-azure.md](docs/deployment-azure.md) — deployed Azure topology,
  release promotion, DNS/TLS, monitoring, and operational limitations
- [docs/adr/](docs/adr/) — architecture decision records

## Conventions

- Conventional Commits (`feat:`, `fix:`, `docs:`, ...), `main` + `feature/*` branches.
- CI gates: restore → build → unit tests → integration tests → migration/schema
  check → security scan (CodeQL, Trivy, Dependabot) → publish verification.
  Successful `main` CI runs trigger the OIDC-based staging image deployment.
- All legal copy is draft: `ENTWURF - anwaltlich pruefen lassen`.
