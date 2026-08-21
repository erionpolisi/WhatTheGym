# Azure deployment (prepared, not executed)

Nothing is deployed yet. Local Docker Compose is the mandatory first target;
this document plus `infrastructure/azure/` prepare the later rollout. CI
publish only builds and verifies artifacts/images — it does not push or deploy
until an Azure Container Registry and credentials are deliberately configured.

## Target topology

| Concern | Service | Tier | Est. cost/month |
| ------- | ------- | ---- | ---------------- |
| Frontend | Azure Static Web Apps | Free | 0 EUR |
| API | Azure Container Apps | Consumption, scale-to-zero (0–1 replicas) | ~0–5 EUR |
| Database | see ADR 0008 | external free PostgreSQL (default) or Azure PG Flexible B1ms | 0 EUR / ~14–17 EUR |
| Secrets | Azure Key Vault | Standard | ~0 EUR |
| Telemetry | Log Analytics + App Insights | 0.1 GB/day cap | ~0–2 EUR |

Default (cost cap ≤ 10 EUR/month): hybrid with an external managed PostgreSQL
free tier. The all-Azure variant (`deployPostgres=true`) exceeds the cap and is
documented in [adr/0008-azure-cost-plan.md](adr/0008-azure-cost-plan.md).

## Environments

| Environment | Frontend | API |
| ----------- | -------- | --- |
| Staging | staging.whatthegym.at | api-staging.whatthegym.at |
| Production | whatthegym.at | api.whatthegym.at |

## Rollout steps (when going live)

1. Create resource groups `wtg-staging-rg` / `wtg-production-rg` and an Azure
   Container Registry; wire `AcrPush` credentials into GitHub Actions secrets.
2. Extend the CI `publish` job to push `whatthegym-api:<sha>` (deliberate,
   separate change — currently build/verify only).
3. Provision per environment:
   `az deployment group create -g wtg-staging-rg -f infrastructure/azure/main.bicep -p @infrastructure/azure/parameters.staging.json`
   supplying the secure parameters (`externalPostgresConnectionString` or
   `postgresAdminPassword`).
4. Configure secrets in Key Vault / Container Apps: Google OAuth client
   (redirect URI `https://api-<env>.whatthegym.at/api/v1/auth/google/callback`),
   Resend API key, `Auth:BootstrapAdminEmail`, `Analytics:HashSecret`.
5. Point DNS (CNAMEs) at the Static Web App and Container App, add custom
   domains + managed certificates.
6. Deploy the frontend to Static Web Apps with
   `NEXT_PUBLIC_API_BASE_URL=https://api-<env>.whatthegym.at`.
7. Verify `/health/ready`, Swagger, Google login, mail delivery, and the CORS
   allowlist; then run the production smoke checklist.

## Explicitly out of scope for the MVP

Kubernetes, microservices, message brokers, PostGIS/geo search, image storage,
CDN beyond SWA defaults, multi-region HA.
