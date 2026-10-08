# Azure deployment

Verified snapshot: 2026-10-08.

Staging and Production are deployed in Germany West Central. Local Docker
Compose remains the mandatory first target. CI verifies changes; after CI
succeeds on `main`, `deploy-staging.yml` builds immutable API and frontend
images, pushes them to ghcr.io, and updates both staging Container Apps through
secretless GitHub OIDC.

## Target topology

| Concern | Service | Tier | Est. cost/month |
| ------- | ------- | ---- | ---------------- |
| Frontends | Azure Container Apps | Consumption, scale-to-zero (0–1 replica each) | ~0–2 EUR |
| APIs | Azure Container Apps | Consumption, scale-to-zero (0–1 replica each) | ~0–5 EUR |
| Database | see ADR 0008 | external free PostgreSQL (default) or Azure PG Flexible B1ms | 0 EUR / ~14–17 EUR |
| Secrets | Azure Key Vault | Standard | ~0 EUR |
| Data Protection | Azure Blob Storage + Key Vault RSA key | Standard LRS, a few small key files | <1 EUR |
| Telemetry | Log Analytics + App Insights | 0.1 GB/day cap | ~0–2 EUR |
| Registry | GitHub Container Registry (ghcr.io) | Free | 0 EUR (ADR 0008 addendum; no ACR) |

Default (cost cap ≤ 10 EUR/month): hybrid with an external managed PostgreSQL
free tier. The all-Azure variant (`deployPostgres=true`) exceeds the cap and is
documented in [adr/0008-azure-cost-plan.md](adr/0008-azure-cost-plan.md).

## Environments

| Environment | Resource group | Frontend | API | Database |
| ----------- | -------------- | -------- | --- | -------- |
| Staging | `wtg-staging` | `staging.whatthegym.at` | `api-staging.whatthegym.at` | Neon `WhatTheGym - staging` (`square-sun-28378821`), branch `staging` |
| Production | `wtg-prod` | `whatthegym.at` | `api.whatthegym.at` | Neon `WhatTheGym - prod` (`royal-base-40974476`), branch `production` |

The Azure for Students subscription currently permits one Container Apps
environment globally. Until that quota is raised, Production reuses
`wtg-staging-cae` while remaining separate at the application and data layers:
`wtg-prod-api` and `wtg-prod-web` have their own identities, Key Vault,
configuration, revisions, domains, and Neon project. The shared boundary is
limited to the Container Apps network/runtime and its container-log
destination. Set `existingContainerAppsEnvironmentName` and
`existingContainerAppsEnvironmentResourceGroup` to reuse the environment;
leave both empty to create a dedicated environment after quota approval.

### Deployed Azure resources

The shared runtime is `wtg-staging-cae` in `wtg-staging`. It hosts four
independent Container Apps:

| App | Purpose | Public Azure hostname |
| --- | --- | --- |
| `wtg-staging-web` | Staging Next.js frontend | `wtg-staging-web.salmoncliff-fa4e9f73.germanywestcentral.azurecontainerapps.io` |
| `wtg-staging-api` | Staging ASP.NET Core API | `wtg-staging-api.salmoncliff-fa4e9f73.germanywestcentral.azurecontainerapps.io` |
| `wtg-prod-web` | Production Next.js frontend | `wtg-prod-web.salmoncliff-fa4e9f73.germanywestcentral.azurecontainerapps.io` |
| `wtg-prod-api` | Production ASP.NET Core API | `wtg-prod-api.salmoncliff-fa4e9f73.germanywestcentral.azurecontainerapps.io` |

Environment-specific supporting resources are:

| Environment | API identity | Key Vault | Log Analytics | Application Insights | Data Protection storage |
| --- | --- | --- | --- | --- | --- |
| Staging | `wtg-staging-api-identity` | `wtg-staging-kv` | `wtg-staging-logs` | `wtg-staging-insights` | Declared in Bicep but not yet reconciled |
| Production | `wtg-prod-api-identity` | `wtg-prod-kv` | `wtg-prod-logs` | `wtg-prod-insights` | `wtgpzk43fds4oqy6kdp` |

Both Production apps were initially bootstrapped from staged commit
`b74ab3e02fe4fb5cb9c921ecccbb2545d6da8af9`; both report
`provisioningState=Succeeded`, use scale `0–1`, and the API readiness endpoint
returns HTTP 200. Subsequent releases use the digest-based promotion workflow.

Each environment has its own API identity and Key Vault. Production additionally
uses storage account `wtgpzk43fds4oqy6kdp`, container `data-protection`, for
the ASP.NET Core Data Protection key ring. The generated account name is
intentionally opaque: Azure Storage names must be globally unique, lowercase,
and at most 24 characters. Public blob access and shared-key authentication are
disabled. This resource is required for stable authentication across
scale-to-zero and revision replacement and stores only a few small key files.

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

GitHub Enterprise Managed User repositories issue a different OIDC subject
than standard GitHub repositories. Each Azure application therefore has two
otherwise identical environment-scoped federated credentials:

- standard: `repo:erionpolisi/WhatTheGym:environment:<environment>`
- EMU: `repo:erionpolisi@189998027/WhatTheGym@1341723466:environment:<environment>`

Issuer is `https://token.actions.githubusercontent.com`; audience is
`api://AzureADTokenExchange`. Do not broaden either credential to all branches
or environments.

The public GHCR packages `whatthegym-api` and `whatthegym-web` grant the
`WhatTheGym` repository **Write** under **Manage Actions access**. Staging
publishes with `GITHUB_TOKEN`; no long-lived PAT is required by either
deployment workflow. Production only pulls/promotes existing images.

Pull the production Neon credentials into the ignored local environment file
without changing the staging link:

```powershell
$env:NODE_OPTIONS='--use-system-ca'
neon env pull --project-id <production-project-id> --branch production `
  --file .env.production.local -e DATABASE_URL -e DATABASE_URL_UNPOOLED
```

Then run
`infrastructure/azure/deploy-production.ps1 -SourceSha <staged-commit-sha>`.
It reads the direct Neon URL locally, converts its URI components to a quoted
Npgsql connection string, prompts for the Google client secret, uses the
immutable images built for that staged commit, generates the analytics secret,
and passes all values as secure deployment parameters. Azure stores the
database connection, Google secret, analytics secret, and optional Resend key
in the environment-specific Key Vault; the API accesses them through its
managed identity.

Before deploying, run the read-only preflight check for the target environment:

```powershell
infrastructure/azure/test-prerequisites.ps1 -Environment staging
infrastructure/azure/test-prerequisites.ps1 -Environment production
```

## Provisioning and reconciliation procedure

1. Use resource groups `wtg-staging` / `wtg-prod` (names match TASKS and
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
3. Provision or reconcile each environment:
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
5. Point DNS at the frontend and API Container Apps, add custom
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

## Production DNS, custom domains, and TLS

At this snapshot, the Production DNS changes, Container Apps hostname bindings,
and managed certificates are still pending. The default Azure endpoints are
operational.

The Container Apps environment has static IP `4.182.7.34`. The Container Apps
domain verification ID is:

`CB83829BB23AECE9D2B3BE2CE68C331A52DECB239C4F9F025192CEF200D98F94`

Configure these records at easyname:

| Type | Host | Value |
| --- | --- | --- |
| A | `@` (zone apex) | `4.182.7.34` |
| TXT | `asuid` | verification ID above |
| CNAME | `api` | `wtg-prod-api.salmoncliff-fa4e9f73.germanywestcentral.azurecontainerapps.io` |
| TXT | `asuid.api` | verification ID above |

Replace the current apex A record (`91.151.18.21`); do not add a normal CNAME
at the zone apex. Existing MX and unrelated TXT records remain unchanged.
After propagation, bind the domains and let Azure create free managed
certificates:

```powershell
az containerapp hostname bind -g wtg-prod -n wtg-prod-web `
  --hostname whatthegym.at --validation-method TXT
az containerapp hostname bind -g wtg-prod -n wtg-prod-api `
  --hostname api.whatthegym.at --validation-method CNAME
```

DNS only routes the names to Azure. The hostname bindings select the correct
Container App, and the managed certificates enable HTTPS and renew
automatically. Both are required for Google OAuth and `Secure` cookies.

The Production Google Web OAuth client must allow exactly:

`https://api.whatthegym.at/api/v1/auth/google/callback`

## Monitoring and cost boundaries

The Container Apps environment owns its container-log destination. Because all
four apps share `wtg-staging-cae`, their console and platform logs currently
flow to `wtg-staging-logs`, regardless of resource group. Queries must filter
by Container App name (`wtg-staging-*` versus `wtg-prod-*`). The misleading
workspace name is accepted until a second Container Apps environment is
available.

Both Log Analytics workspaces have 30-day retention and a 0.1 GB/day cap.
`wtg-prod-insights` is linked to `wtg-prod-logs`, but the API currently only
receives its connection string; it does not yet include an Application
Insights/OpenTelemetry SDK. Consequently:

- Container console/platform logs are active in `wtg-staging-logs`.
- `wtg-prod-insights` does not yet provide complete request, dependency,
  exception, or performance telemetry.
- `wtg-staging-insights` and its automatically created Smart Detection
  resource are not required for day-to-day staging testing and may be removed
  after Bicep is changed not to recreate them.

Before public go-live, wire production API telemetry to
`wtg-prod-insights`, enable sampling, and add an availability test plus alert
for `/health/ready`. Monitoring resources have no meaningful fixed compute
charge; ingestion drives cost. The caps prevent unexpected log spend.

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

Production transactional email is not ready until a Resend account, API key,
verified sending domain, and required DNS records are configured. Without
Resend the fallback sender only logs mail; that is acceptable for technical
deployment testing but not for public operation or legal notifications.
