# Onboarding

## What you need

- Docker Desktop (mandatory for the database and integration tests)
- .NET 8 SDK (a newer SDK also builds `net8.0`)
- Node.js 20+

No cloud accounts, Google OAuth, Resend, or DNS access are required locally.

## First run (10 minutes)

```bash
git clone <repo>
cd WhatTheGym
cp .env.example .env
docker compose up --build          # API on http://localhost:7001 (+ Swagger)
```

In a second terminal:

```bash
cd frontend
cp .env.local.example .env.local
npm install
npm run dev                        # http://localhost:3000
```

Open http://localhost:3000, browse the seeded Vienna studios, then go to
"Mein Konto" → "Lokaler Dev-Login" and sign in with `admin@example.invalid`
to become the bootstrap Admin. Any other email creates a normal user.

## Development loops

- Backend only: `docker compose up db`, then
  `dotnet run --project backend/src/Gym.Api` (https://localhost:7001).
- New migration:
  `cd backend && dotnet ef migrations add <Name> --project src/Gym.Infrastructure --startup-project src/Gym.Api`
- Tests: `dotnet test backend/tests/<project>` (integration tests spin up their
  own PostgreSQL container via Testcontainers).
- Admin operations happen through Swagger — there is intentionally no admin UI.

## Configuration model

Configuration flows `appsettings.json` → `appsettings.<Environment>.json` →
environment variables (compose uses `Section__Key=value`). Key settings:

| Setting | Purpose |
| ------- | ------- |
| `ConnectionStrings:Postgres` | Database |
| `Database:MigrateOnStartup` | Apply migrations + seed on boot |
| `Seed:SeedCatalog` / `Seed:SeedDemoData` | Vienna catalogue / demo data (demo only in Development) |
| `Auth:GoogleClientId/Secret` | Google OIDC (empty locally) |
| `Auth:EnableDevLogin` | Dev-login endpoint (Development only) |
| `Auth:BootstrapAdminEmail` | First-admin bootstrap |
| `Cors:AllowedOrigins` | Environment CORS allowlist |
| `Mail:ResendApiKey` | Empty = mails are logged, not sent |
| `Retention:*` | Retention periods (years/days) |

## Definition of done

Build clean (warnings are errors), tests green (`Domain`, `Application`,
`IntegrationTests`), migration model check passes
(`dotnet ef migrations has-pending-model-changes`), docs/ADRs updated for
meaningful decisions, Conventional Commit messages.
