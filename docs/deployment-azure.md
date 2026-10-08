# Azure deployment

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

The Azure for Students subscription currently permits one Container Apps
environment globally. Until that quota is raised, Production reuses
`wtg-staging-cae` while remaining separate at the application and data layers:
`wtg-prod-api` and `wtg-prod-web` have their own identities, Key Vault,
configuration, revisions, domains, and Neon project. The shared boundary is
limited to the Container Apps network/runtime and its container-log
destination. Set `existingContainerAppsEnvironmentName` and
`existingContainerAppsEnvironmentResourceGroup` to reuse the environment;
leave both empty to create a dedicated environment after quota approval.

## Promoting staging to production

Production is not swapped with staging at the DNS, resource, or database
level. Both environments remain separate. A release promotes the same tested
source revision:

1. A successful `main` CI run builds the API image once and builds two
   frontend variants from the same commit. The variants differ only in their
   compile-time public API/site URLs.
2. Staging deploys both images by immutable digest. Only after both revisions
   are provisioned does it publish a 90-day release-manifest artifact
   containing the source SHA and the exact API/staging/production digests.
3. Creating a semantic release tag such as `v1.0.0` on that exact `main`
   commit starts `deploy-production.yml`.
4. The workflow rejects commits without a valid, non-expired staging release
   manifest or available digest-addressed images. The protected GitHub
   `production` environment then requires manual approval.
5. Production deploys the exact API and production-frontend digests recorded
   by staging. No image is rebuilt or resolved through a mutable tag during
   the production release. A failed rollout restores the previous image pair.

Production requires its own Neon database, Google callback configuration,
secrets, Container Apps, and OIDC identity. Configure the GitHub `production`
environment with required reviewers and environment variables
`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID`. The
production identity must have Contributor only on `wtg-prod`.

Pull the production Neon credentials into the ignored local environment file
without changing the staging link:

```powershell
$env:NODE_OPTIONS='--use-system-ca'
neon env pull --project-id <production-project-id> --branch production `
  --file .env.production.local -e DATABASE_URL -e DATABASE_URL_UNPOOLED
```

Then run `infrastructure/azure/deploy-production.ps1`. It reads the direct
Neon URL locally, prompts for the Google client secret, generates the analytics
secret, and passes all values as secure deployment parameters. Azure stores
the database connection, Google secret, analytics secret, and optional Resend
key in the environment-specific Key Vault; the API accesses them through its
managed identity.

Before deploying, run the read-only preflight check for the target environment:

```powershell
infrastructure/azure/test-prerequisites.ps1 -Environment staging
infrastructure/azure/test-prerequisites.ps1 -Environment production
```

## Rollout steps (when going live)

1. Create resource groups `wtg-staging` / `wtg-prod` (names match TASKS and
   TODO_NOW). Container images go to **ghcr.io** — no Azure Container
   Registry (cost decision, ADR 0008 addendum). If the ghcr package stays
   private, add a `registries` block (username + PAT secret) to the container
   app in Bicep; a public package needs no change.
2. Staging uses a GitHub OIDC federated identity scoped to the
   `wtg-staging` resource group. `deploy-staging.yml` runs after successful CI
   on `main`, publishes both images with the commit SHA, updates the API first,
   waits for a provisioned revision, and then updates the frontend. Production
   uses release tags, a protected GitHub environment, and a separate identity
   scoped to `wtg-prod`.
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

The API's ASP.NET Core Data Protection key ring is persisted in a private Blob
container and protected with a Key Vault RSA key. Access uses the API's
user-assigned managed identity; storage account keys are disabled. Verify
Google login across scale-to-zero and a revision replacement before go-live
(ADR 0003).
