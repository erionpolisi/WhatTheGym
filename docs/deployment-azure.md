# Azure deployment (prepared, not executed)

Nothing is deployed yet. Local Docker Compose is the mandatory first target;
this document plus `infrastructure/azure/` prepare the later rollout. CI
builds and verifies artifacts/images only — there is no push and no deploy
workflow until the ghcr.io registry and Azure OIDC credentials are
deliberately configured (TASKS Phase 2).

## Target topology

| Concern | Service | Tier | Est. cost/month |
| ------- | ------- | ---- | ---------------- |
| Frontend | Azure Static Web Apps | Free | 0 EUR |
| API | Azure Container Apps | Consumption, scale-to-zero (0–1 replicas) | ~0–5 EUR |
| Database | see ADR 0008 | external free PostgreSQL (default) or Azure PG Flexible B1ms | 0 EUR / ~14–17 EUR |
| Secrets | Azure Key Vault | Standard | ~0 EUR |
| Telemetry | Log Analytics + App Insights | 0.1 GB/day cap | ~0–2 EUR |
| Registry | GitHub Container Registry (ghcr.io) | Free | 0 EUR (ADR 0008 addendum; no ACR) |

Default (cost cap ≤ 10 EUR/month): hybrid with an external managed PostgreSQL
free tier. The all-Azure variant (`deployPostgres=true`) exceeds the cap and is
documented in [adr/0008-azure-cost-plan.md](adr/0008-azure-cost-plan.md).

## Environments

| Environment | Frontend | API |
| ----------- | -------- | --- |
| Staging | staging.whatthegym.at | api-staging.whatthegym.at |
| Production | whatthegym.at | api.whatthegym.at |

## Rollout steps (when going live)

1. Create resource groups `wtg-staging` / `wtg-prod` (names match TASKS and
   TODO_NOW). Container images go to **ghcr.io** — no Azure Container
   Registry (cost decision, ADR 0008 addendum). If the ghcr package stays
   private, add a `registries` block (username + PAT secret) to the container
   app in Bicep; a public package needs no change.
2. Add deploy workflows (deliberate, separate change — CI currently only
   builds/verifies): GitHub OIDC federated identity to Azure (no static
   secrets), `deploy-staging.yml` (on `main` after CI: build image → push
   `ghcr.io/<owner>/whatthegym-api:<sha>` → `az containerapp update`),
   `deploy-production.yml` (`workflow_dispatch`/`v*` tag only, never
   automatic).
3. Provision per environment:
   `az deployment group create -g wtg-staging -f infrastructure/azure/main.bicep -p @infrastructure/azure/parameters.staging.json`
   supplying the secure parameters (`externalPostgresConnectionString` or
   `postgresAdminPassword`, `googleClientSecret`, `analyticsHashSecret`,
   `resendApiKey`).
4. Runtime configuration is wired by Bicep as container-app env/secrets:
   Google OAuth client (redirect URI
   `https://api-<env>.whatthegym.at/api/v1/auth/google/callback`), Resend API
   key, `Auth:BootstrapAdminEmail`, `Mail:PublicBaseUrl`,
   `Analytics:HashSecret`, and `ForwardedHeaders:Enabled=true` (the ingress
   terminates TLS; the app needs `X-Forwarded-For/Proto` for per-client rate
   limiting and correct OIDC redirect URIs).
5. Point DNS (CNAMEs) at the Static Web App and Container App, add custom
   domains + managed certificates. **Same-site domains are mandatory**: the
   session cookies are `SameSite=Lax`, so frontend (`whatthegym.at`) and API
   (`api.whatthegym.at`) must share a registrable domain — the default
   `*.azurestaticapps.net` / `*.azurecontainerapps.io` hostnames are
   cross-site and will not work for login.
6. Deploy the frontend to Static Web Apps with
   `NEXT_PUBLIC_API_BASE_URL=https://api-<env>.whatthegym.at`.
   **Validate early**: the frontend uses SSR/ISR (dynamic routes); SWA's
   hybrid Next.js support is preview-quality. If staging surfaces blockers,
   the documented fallback is hosting the frontend as a second scale-to-zero
   container app (~0–2 EUR, still within cap) — decide via ADR.
7. Verify `/health/ready`, Swagger, Google login, mail delivery, and the CORS
   allowlist; then run the production smoke checklist.

Note on telemetry: Bicep passes `APPLICATIONINSIGHTS_CONNECTION_STRING`, but
the API does not bundle the App Insights SDK. Active out of the box:
console logs → Log Analytics and availability tests on `/health/ready`.
Add the SDK/agent later if request-level telemetry is wanted.

## Explicitly out of scope for the MVP

Kubernetes, microservices, message brokers, PostGIS/geo search, image storage,
CDN beyond SWA defaults, multi-region HA.

## Known limitation: scale-to-zero vs. background services

The container app scales to zero (cost decision, ADR 0008/0012). The hosted
background services — email outbox processor and daily retention sweeper —
only run while an instance is warm. Pending outbox mails and retention sweeps
are picked up when the next request wakes the app. Acceptable for the MVP
traffic profile; revisit (minReplicas 1 or a scheduled job) before legal mail
latency becomes a compliance concern.
