# Onboarding

## What you need

- Docker Desktop (mandatory for the database and integration tests)
- .NET 8 SDK (a newer SDK also builds `net8.0`)
- Node.js 20+

Public browsing needs no cloud credentials. Authenticated local workflows use
the existing staging Google OAuth client. Resend remains optional locally.

## First run (10 minutes)

```bash
git clone <repo>
cd WhatTheGym
cp .env.example .env
                                                      # add GOOGLE_CLIENT_SECRET for login
docker compose up --build          # Website on :3000, API/Swagger on :7001
```

For frontend-only development with hot reload:

```bash
cd frontend
cp .env.local.example .env.local
npm install
npm run dev                        # http://localhost:3000
```

Before the first login:

1. In Google Cloud, add
  `http://localhost:7001/api/v1/auth/google/callback` to the staging web
  client's authorized redirect URIs.
2. Put that client's secret in `GOOGLE_CLIENT_SECRET` in the ignored `.env`.
3. Set `BOOTSTRAP_ADMIN_EMAIL` to the verified Google email that should become
  the first local Admin.

Open http://localhost:3000, go to "Mein Konto", and select
"Mit Google anmelden". The frontend receives only HttpOnly application cookies;
Google and application tokens never enter browser storage.

## Development loops

- Full parity/sandbox: `docker compose up --build` runs immutable app images
  with normal PostgreSQL. Use this before pushing and for end-to-end checks.
- Backend only: `docker compose up db`, then
  `dotnet run --project backend/src/Gym.Api` (https://localhost:7001).
- Frontend hot reload: `npm --prefix frontend run dev`; keep API/database in
  Docker or run the API on the host. Compose itself intentionally has no source
  bind mounts or watch mode.
- New migration:
  `cd backend && dotnet ef migrations add <Name> --project src/Gym.Infrastructure --startup-project src/Gym.Api`
- Tests: `dotnet test backend/tests/<project>` (integration tests spin up their
  own PostgreSQL container via Testcontainers).
- Manual/exploratory testing and administration: open `backend/http/` in Rider
  (JetBrains HTTP Client) — per-area request files for every endpoint with
  environment switching (local/staging/production) and captured tokens. See
  `backend/http/README.md`.
- Admin operations happen through Swagger or `backend/http/` — there is
  intentionally no admin UI.

## Configuration model

Configuration flows `appsettings.json` → `appsettings.<Environment>.json` →
environment variables (compose uses `Section__Key=value`). Key settings:

| Setting | Purpose |
| ------- | ------- |
| `ConnectionStrings:Postgres` | Database |
| `Database:MigrateOnStartup` | Apply migrations + seed on boot |
| `Seed:SeedCatalog` / `Seed:SeedDemoData` | Vienna catalogue / demo data (demo only in Development) |
| `Auth:GoogleClientId/Secret` | Google OIDC (local reuses the staging client) |
| `Auth:BootstrapAdminEmail` | First-admin bootstrap |
| `Cors:AllowedOrigins` | Environment CORS allowlist |
| `Mail:ResendApiKey` | Empty = mails are logged, not sent |
| `Mail:FromAddress/FromName` | Verified sender and display name |
| `Retention:*` | Retention periods (years/days) |

## Resend setup

Use a dedicated transactional subdomain so mail reputation is isolated from
the root domain. Secrets stay in `.env` locally and Key Vault in Azure.

1. Create a Resend account at https://resend.com/signup.
2. Add `mail.whatthegym.at` under Domains and select an EU sending region when
  available.
3. Add every DNS record shown by Resend to easyname exactly as displayed.
  Do not replace unrelated root-domain MX or SPF records. Wait until Resend
  reports the subdomain as verified.
4. Create separate sending-only, domain-restricted API keys for local/staging
  and production. A key value is shown only once.
5. Put the local key in `RESEND_API_KEY` in the ignored `.env`. Keep
  `MAIL_FROM_ADDRESS=noreply@mail.whatthegym.at`.
6. Recreate the API container: `docker compose up --build -d api`.
7. Submit the contact form with an inbox you control. Verify the API outbox row
  becomes `Sent`, the message appears in Resend Logs, and it arrives in the
  inbox. On failure, inspect `docker compose logs --tail 100 api`; retries are
  persisted by the outbox.
8. Supply the staging/production keys only through the existing secure Bicep
  parameter/deploy script flow. Never put them in parameter JSON or GitHub
  variables that are not marked secret.

Without `RESEND_API_KEY`, local development deliberately uses the logging
sender. This is the preferred daily loop; enable real delivery only for mail
integration checks to avoid accidental sends and quota use.

## Definition of done

Build clean (warnings are errors), tests green (`Domain`, `Application`,
`IntegrationTests`), migration model check passes
(`dotnet ef migrations has-pending-model-changes`), docs/ADRs updated for
meaningful decisions, Conventional Commit messages.
