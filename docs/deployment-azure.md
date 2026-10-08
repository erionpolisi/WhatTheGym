# Azure deployment (prepared, not executed)

Staging is deployed in `wtg-staging` in Germany West Central. Local Docker
Compose remains the mandatory first target. CI verifies changes; after CI
succeeds on `main`, `deploy-staging.yml` builds immutable API and frontend
images, pushes them to ghcr.io, and updates both Container Apps through
secretless GitHub OIDC.

## Target topology

| Concern | Service | Tier | Est. cost/month |
| ------- | ------- | ---- | ---------------- |
| Frontend | Azure Container Apps | Consumption, scale-to-zero (0–1 replicas) | ~0–2 EUR |
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
2. Staging uses a GitHub OIDC federated identity scoped to the
   `wtg-staging` resource group. `deploy-staging.yml` runs after successful CI
   on `main`, publishes both images with the commit SHA, updates the API first,
   waits for a healthy revision, and then updates the frontend. Production
   deployment remains manual and must use a separate identity and workflow.
3. Provision per environment:
   `az deployment group create -g wtg-staging -f infrastructure/azure/main.bicep -p @infrastructure/azure/parameters.staging.json`
   supplying the secure parameters (`externalPostgresConnectionString` or
   `postgresAdminPassword`, `googleClientSecret`, `analyticsHashSecret`,
   `resendApiKey`).
   For staging, `infrastructure/azure/deploy-staging.ps1` reads the linked
   Neon's unpooled URL from the ignored `.env.local`, prompts for the Google
   client secret, generates the analytics secret in memory, and passes all
   secure values directly to Azure without writing them to the repository.
4. Runtime configuration is wired by Bicep as container-app env/secrets:
   Google OAuth client (redirect URI
   `https://api-<env>.whatthegym.at/api/v1/auth/google/callback`), Resend API
   key, `Auth:BootstrapAdminEmail`, `Mail:PublicBaseUrl`,
   `Analytics:HashSecret`, and `ForwardedHeaders:Enabled=true` (the ingress
   terminates TLS; the app needs `X-Forwarded-For/Proto` for per-client rate
   limiting and correct OIDC redirect URIs).
5. Point DNS (CNAMEs) at the frontend and API Container Apps, add custom
   domains + managed certificates. **Same-site domains are mandatory**: the
   session cookies are `SameSite=Lax`, so frontend (`whatthegym.at`) and API
   (`api.whatthegym.at`) must share a registrable domain — the default
   `*.azurestaticapps.net` / `*.azurecontainerapps.io` hostnames are
   cross-site and will not work for login.
6. Build the frontend image with
   `NEXT_PUBLIC_API_BASE_URL=https://api-<env>.whatthegym.at` and deploy it
   as the second scale-to-zero Container App. The Azure for Students region
   policy may not allow Static Web Apps in any permitted region; Container
   Apps preserve full Next.js SSR support and stay within the cost cap.
7. Verify `/health/ready`, Swagger, Google login, mail delivery, and the CORS
   allowlist; then run the production smoke checklist.

Note on telemetry: Bicep passes `APPLICATIONINSIGHTS_CONNECTION_STRING`, but
the API does not bundle the App Insights SDK. Active out of the box:
console logs → Log Analytics and availability tests on `/health/ready`.
Add the SDK/agent later if request-level telemetry is wanted.

## Explicitly out of scope for the MVP

Kubernetes, microservices, message brokers, PostGIS/geo search, image storage,
CDN, multi-region HA.

## Known limitation: scale-to-zero vs. background services

The container app scales to zero (cost decision, ADR 0008/0012). The hosted
background services — email outbox processor and daily retention sweeper —
only run while an instance is warm. Pending outbox mails and retention sweeps
are picked up when the next request wakes the app. Acceptable for the MVP
traffic profile; revisit (minReplicas 1 or a scheduled job) before legal mail
latency becomes a compliance concern.

The API's ASP.NET Core Data Protection key ring is also currently ephemeral in
staging. A cold replacement can invalidate login/correlation cookies. Persist
and protect the key ring in Azure before production, then verify Google login
across scale-to-zero and a revision replacement (ADR 0003).
