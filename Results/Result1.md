# WhatTheGym — Implementation Result 1

Date: 2026-08-21 · Executed: full Prompt.md delivery (Phases 1–7)
Status: **Complete — all phases implemented and locally validated**

## 1. Summary

The WhatTheGym MVP was implemented end to end as specified by CONSTRAINTS.md /
AGENTS.md / Prompt.md: a .NET 8 Clean Architecture monolith (Domain,
Application, Infrastructure, Api + 3 test projects), PostgreSQL with EF Core
migrations and PostgreSQL-native search, Google-only BFF auth with a local
dev fallback, reviews/scoring with materialized summaries, the complete
LegalCase flow, privacy/GDPR endpoints, a persistent mail outbox, retention
sweeper, a thin SEO-aware Next.js frontend, Docker Compose, CI/security
pipelines, documentation with 9 ADRs, and prepared (undeployed) Azure Bicep.

## 2. Validation results (all executed locally)

| Check | Result |
| ----- | ------ |
| `dotnet build WhatTheGym.sln` (warnings as errors) | 0 errors, 0 warnings |
| Unit tests `Gym.Domain.Tests` | **40/40 passed** |
| Unit tests `Gym.Application.Tests` | **22/22 passed** |
| Integration tests `Gym.IntegrationTests` (Testcontainers PostgreSQL, real HTTP) | **33/33 passed** |
| EF model drift `dotnet ef migrations has-pending-model-changes` | no pending changes |
| `docker compose up --build` | db healthy, API up, migrations applied |
| Seed | 50 Vienna gyms, 10 chains, 12 amenities, 3 legal-doc drafts, demo data (Dev only) |
| Smoke: `/health/live`, `/health/ready`, Swagger | 200 |
| Smoke: search `term=mcfit` (FTS/trigram) | 5 hits |
| Smoke: dev-login bootstrap admin → create review → summary | role=Admin, review Published, `scoreBasis both`, total 4.5 |
| Demo scores (recomputed seed) | `membershipOnly 4.5` / `both 4.01` — matches hand-calculated values |
| Frontend `npm run build` + `next start` against live API | studios list, detail (JSON-LD + score "4,01"), Impressum (ENTWURF), sitemap with 50 gym URLs, robots.txt — all verified |

Integration coverage includes: catalogue/search/filters/ETags, review
lifecycle incl. revisions + score updates, auth (refresh rotation, logout
revocation, role enforcement), full legal flow (report → status token →
fast-track hide → decide → tokenized appeal → reversal reinstate → close →
export → transparency), moderation (reason-required removal, admin restore),
contact honeypot drop, analytics allowlist, rate-limit 429, processing
activities, legal document versioning/publishing.

## 3. What was built (per phase)

1. **Foundation** — solution + 7 projects, `Directory.Build.props` (nullable,
   analyzers, warnings-as-errors), Result pattern, FluentValidation, Serilog +
   correlation IDs, RFC 7807 ProblemDetails, `/api/v1`, Swagger (= admin UI),
   health checks, DI, EF Core + squashed `InitialCreate` migration (incl.
   tsvector column, `pg_trgm`, case-number sequence, audit UPDATE-block
   trigger), Docker Compose (db/api/pgAdmin profile), `.env.example`.
2. **Catalogue & search** — chains/gyms/amenities, stable unique SEO slugs,
   admin CRUD + status rules (Draft hidden, closed = no reviews), pagination,
   district/score/area/chain filters, German FTS + trigram + ILIKE ranking,
   summary endpoints, deterministic idempotent Vienna seed (~50 studios).
3. **Reviews & scoring** — verified-Google-only reviews (≥1 of 11 direct 1–5
   ratings), automatic publication, reversible soft delete (author/moderator/
   admin/account-deletion origins), edit revisions, content sanitization +
   link-spam limits, materialized `GymRatingSummary` recomputed in the same
   unit of work, admin rebuild command, exact 50/50 + `scoreBasis` semantics.
4. **Identity/roles/moderation** — Google OIDC (code flow + PKCE, no token
   persistence) behind config, cookie BFF sessions, rotating hashed refresh
   tokens with reuse detection, CORS allowlists, logout, `/me` endpoints,
   config-based first-admin bootstrap, `User/Moderator/Admin` policies,
   moderator review removal, dev-login (Development-only).
5. **Legal/privacy/mail** — single report path → LegalCase with human-readable
   numbers, append-only audited events (DB-enforced), status machine, normal
   vs fast-track visibility, KeepOnline/FullyRemoved decisions with rationale,
   hashed status/appeal tokens, ≥6-month appeals with reversal handling,
   retention config + legal holds + daily sweeper, transparency report,
   versioned legal documents (ENTWURF), personal data export, account
   anonymization, tested processing-activities record, Resend outbox with
   retry/backoff (logging fallback locally).
6. **Frontend/hardening/docs/CI** — Next.js App Router (SSR/ISR): search with
   filters, gym detail with full score breakdown + schema.org `ExerciseGym` +
   `AggregateRating`, review/report/contact/appeal forms with honeypots,
   Google + dev login, account page (export/delete), backend-driven legal
   pages, case-status page, transparency page, sitemap/robots; rate limits,
   PII-free analytics; GitHub Actions CI (restore → build → unit → integration
   → migration checks → Trivy → publish-verify-only), CodeQL, Dependabot,
   issue templates; docs: architecture/domain/api/scoring/legal/
   processing-activities/onboarding/seo/seed-data/deployment + README.
7. **Azure preparation** — `infrastructure/azure/main.bicep` (Container Apps
   scale-to-zero, Static Web Apps Free, Key Vault, capped Log Analytics/App
   Insights, optional PostgreSQL Flexible), staging/production parameters,
   deployment runbook. Nothing deployed; CI does not push images.

## 4. Exact local startup commands

```bash
# Full stack (no cloud secrets needed)
cp .env.example .env
docker compose up --build          # API + Swagger: http://localhost:7001/swagger

# Frontend
cd frontend
cp .env.local.example .env.local
npm install
npm run dev                        # http://localhost:3000

# Backend without Docker (needs: docker compose up db)
cd backend
dotnet run --project src/Gym.Api   # https://localhost:7001 + http://localhost:5001

# Tests
cd backend
dotnet test tests/Gym.Domain.Tests
dotnet test tests/Gym.Application.Tests
dotnet test tests/Gym.IntegrationTests    # needs Docker (Testcontainers)

# Admin locally: dev-login with admin@example.invalid (BOOTSTRAP_ADMIN_EMAIL)
# Stop stack: docker compose down        (add -v to reset the database)
```

## 5. Documented ADR defaults (docs/adr/)

| ADR | Decision |
| --- | -------- |
| 0001 | Monolith + Clean Architecture, CQRS-light without MediatR |
| 0002 | PostgreSQL FTS (german) + pg_trgm behind `ISearchIndex`/`IGymSearchQuery`; no external search |
| 0003 | OIDC code flow + PKCE, no provider-token persistence, custom rotating hashed refresh tokens with family revocation, Development-only dev-login, config-based first-admin bootstrap |
| 0004 | Single squashed initial migration incl. hand-written SQL (FTS, trigger, sequence) |
| 0005 | Area score = mean of category averages; round 2 decimals at edges; synchronous materialized summaries; one active review per user+gym |
| 0006 | Global case-number sequence `WTG-<year>-<n>`; SHA-256 tokens; appeal token to adversely affected party; author notified only on hide/removal; audit UPDATE blocked at DB level, DELETE only via retention |
| 0007 | Daily retention sweeper (7y case audit, 3y revisions, 400d analytics, 90d outbox); account deletion = anonymize + soft delete, holds always pause |
| 0008 | Cost cap: default hybrid (SWA Free + Container Apps scale-to-zero + external free PostgreSQL ≈ 0–7 €/mo); all-Azure variant (~16–22 €/mo) prepared but documented as over-cap |
| 0009 | Seed provenance: real studios/chains/districts, addresses flagged for verification before production; demo data hard-gated to Development |

## 6. Notable issues found and fixed during validation

- `ScoreBasis` name collision in DTO → qualified enum reference.
- Npgsql NULL parameters in raw search SQL → explicit `NpgsqlDbType`.
- Swagger schema-ID collisions from same-named nested request records → full-name schema ids.
- Score recalculation raced pending EF changes → published-ratings query now merges tracked local state.
- SDK 10 generated `.slnx` → replaced with classic `.sln` for .NET 8 SDK/CI/Docker compatibility.
- Zscaler TLS interception broke NuGet in Docker builds → optional gitignored `backend/certs/` CA hook (no-op on CI), documented.
- Host `obj/` leaked into image → `.dockerignore`.
- Seeder shared one `ReviewRatings` instance across owners (EF owned-type silently lost values) → per-review factories; re-seeded and numerically verified.

## 7. Known limitations / open points (documented, non-blocking)

- Google OAuth, Resend, DNS and Azure are intentionally unconfigured
  (placeholders + `.env.example`); the Google path is code-complete but
  untested against real Google until credentials exist.
- Domain-level error messages are English developer-facing fallbacks; all
  user-triggerable validation paths return German messages.
- Seeded street addresses need verification against official sources before
  staging/production (ADR 0009).
- All legal texts are drafts: `ENTWURF - anwaltlich pruefen lassen`.
- Bicep is prepared but has never been deployed; treat the first
  `az deployment group create` as a validation step.

## 8. Next three practical production steps

1. **Create the Google OAuth client** (redirect URI
   `https://api-staging.whatthegym.at/api/v1/auth/google/callback`), set
   `Auth:GoogleClientId/Secret` + `Auth:BootstrapAdminEmail` in staging
   config, and verify the full login/refresh/logout flow against real Google
   on staging.
2. **Provision staging**: create the resource group + ACR, wire registry
   credentials into the CI publish job (switch from verify-only to push),
   run `az deployment group create` with `parameters.staging.json` (external
   PostgreSQL connection string in Key Vault per ADR 0008), deploy the
   frontend to Static Web Apps, point staging DNS, and smoke-test
   `/health/ready` + Swagger + CORS.
3. **Data and legal go-live gate**: verify/correct all 50 seeded studio
   addresses and opening hours from official sources (ADR 0009), have the
   legal drafts (Impressum, Datenschutz, Nutzungsbedingungen, mail texts,
   processing-activities record) reviewed by a lawyer, publish new versions
   via the admin API, and configure the production Resend key with a
   deliverability test through the outbox.
