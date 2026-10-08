# TODO_NOW.md — Continue Phase 2 (step by step)

Snapshot 2026-10-08 of where Phase 2 stands and exactly what to do next.
Companion to TASKS.md Phase 2; delete this file once the exit gate is met.

## Current state (verified in repo)

| Phase 2 item | State |
| --- | --- |
| Bicep templates | ✅ Staging and Production deployed — four scale-to-zero Container Apps share `wtg-staging-cae`; identities, Key Vaults, databases, and revisions remain separate |
| Parameters | ✅ Staging/Production images, region, domains, Google clients, and admin bootstrap email are configured; secure values are supplied at deploy time |
| CI | ✅ `.github/workflows/ci.yml` builds/tests/scans; successful `main` runs trigger the staging deployment workflow |
| Deploy workflows | ✅ Staging deployment succeeded for `b74ab3e`; production promotes its recorded digests from a semantic release tag and awaits its first rehearsal |
| Registry auth in Bicep | ✅ GHCR packages are public; repository Actions access is `Write`; no runtime registry credentials or workflow PAT required |
| Runbook | ❌ `docs/runbook.md` doesn't exist |
| ADR 0008 | ✅ Addendum done: ghcr.io instead of ACR + cost table (verify numbers against real billing after the first staging month) |
| Security hardening | ✅ ADR 0012 shipped: session revalidation, CSRF (X-CSRF header or JSON content type), forwarded headers, audit-token masking, DB-unique reviews — REST clients/scripts must send `X-CSRF: 1` on body-less authenticated writes |
| Production domains | ⏳ easyname A/TXT/CNAME records, Azure hostname bindings, and managed TLS pending |
| Production mail | ❌ Resend account, verified domain, API key, and DNS records pending |
| Production telemetry | ⏳ Container logs active; Application Insights SDK, availability test, and alerts pending |

Flag: Phase 1's exit gate is met except for the Resend account and verified
sending domain. Staging DNS, managed TLS, and Google OAuth are operational.

---

## Step 0 — Unblock from Phase 1.5 (do first, has lead time)

- [x] Register `whatthegym.at` (easyname/World4You/INWX, ~15–30 EUR/yr).
      DNS propagation and OAuth consent verification both take time — start now.
- [x] Create Google Cloud project + OAuth consent screen (external) + OAuth
      client (free, no Azure dependency)
- [ ] Create Resend account (free tier; domain verification needs Step 0.1 DNS)

## Step 1 — Azure foundation (2.1, ~1 hour, 0 EUR)

- [x] Activate **Azure for Students**; put credit expiry date in calendar
- [x] Install/verify `az` CLI, `az login`
- [x] Create resource groups `wtg-staging` and `wtg-prod`
- [x] Budget alerts in Cost Management: 1 / 5 / 10 EUR forecast on the subscription
- [x] Sign up for **Neon** (or Supabase) free tier, EU region → create staging
      database → keep connection string for Step 3

## Step 2 — Repo changes for ghcr.io + deploy pipeline (main coding work)

- [x] Fill in the GitHub owner in `parameters.staging.json` /
      `parameters.production.json` (`ghcr.io/<your-user>/whatthegym-api:<tag>`)
- [x] Keep GHCR packages public and grant the repository Actions `Write`
      access; no `registries` block or workflow PAT is required
- [x] Set up staging **GitHub OIDC → Azure** federated identity (no static secrets):
      `az ad app create` + service principal + federated credential for
      the GitHub `staging` environment, Contributor on `wtg-staging`
- [x] Create the separate production OIDC identity for the protected GitHub
      `production` environment, Contributor only on `wtg-prod`
- [x] New workflow `deploy-staging.yml`: on `main` push, after CI →
      `docker build` → push to ghcr.io with `GITHUB_TOKEN` →
      `az containerapp update -n wtg-staging-api -g wtg-staging --image ghcr.io/...:<sha>`
- [x] New workflow `deploy-production.yml`: semantic `v*.*.*` tag trigger,
      staged-image verification, protected environment approval, and rollback

## Step 3 — First staging deployment (validates the never-executed Bicep)

- [x] Deploy (database, Google, and analytics secure params are required;
      Resend remains optional only while the environment is not public):
      ```
      az deployment group create -g wtg-staging -f infrastructure/azure/main.bicep `
        -p '@infrastructure/azure/parameters.staging.json' `
        -p externalPostgresConnectionString='<Neon connection string>' `
        -p googleClientSecret='<oauth secret>' `
        -p analyticsHashSecret='<random 32+ chars>' `
        -p resendApiKey='<resend key>'
      ```
      (`googleClientId`, `bootstrapAdminEmail`, `publicBaseUrl` are plain
      values in the parameters file — fill them in first.)
      First deployment required iteration for Key Vault references, role
      propagation, region policy, and Container Apps environment mode.
- [x] Push a first image manually to ghcr.io so the Container App has something
      to pull; verify `/health/live` and `/health/ready` on the outputted `apiUrl`
- [x] Verify migrations ran + catalog seeded, no demo data
      (`Seed__SeedDemoData=false` is already in the Bicep)
- [x] Host Next.js as a second Container App after Static Web Apps proved
      unavailable in the subscription's permitted regions
- [x] Point `api-staging.whatthegym.at` + `staging.whatthegym.at`,
      add custom domains/managed certs, then test real Google login —
      **login cannot work on the default hostnames** (`SameSite=Lax` needs
      same-site frontend + API, see docs/deployment-azure.md)

## Immediate next steps — finish Production

- [ ] Replace the easyname apex A record with `4.182.7.34`
- [ ] Add `asuid`, `api`, and `asuid.api` records exactly as documented in
      `docs/deployment-azure.md`
- [ ] Bind `whatthegym.at` to `wtg-prod-web` and
      `api.whatthegym.at` to `wtg-prod-api`; issue managed certificates
- [ ] Verify Production Google login and bootstrap Admin
- [ ] Reconcile Staging Bicep so its Data Protection keys are also persistent
- [ ] Configure Resend and verify its sending-domain DNS before public launch
- [ ] Add Production Application Insights/OpenTelemetry, sampling, readiness
      availability test, and email alert
- [ ] Create the first semantic release tag and rehearse promotion + rollback

## Step 4 — Rehearse rollback (2.3, while nothing matters)

- [ ] Deploy image tag A, then tag B, then roll back:
      `az containerapp update --image ...:<tagA>`
      (or `az containerapp revision activate`)
- [ ] Write the exact commands down — first runbook entry

## Step 5 — Monitoring (2.4, ~30 min)

- [ ] App Insights availability test on `/health/ready` + alert rule → your
      email. Add Application Insights/OpenTelemetry to Production for request,
      dependency, exception, and performance telemetry; shared Container Apps
      console logs alone are not sufficient for Production observability
- [ ] Write 3–4 SQL queries (page views/day, reviews/day, top gyms, stuck
      outbox mails) — these go in the runbook
- [ ] Scale-to-zero caveat (ADR 0012): outbox mails/retention sweeps only run
      while an instance is warm — add a runbook query for stuck `Pending`
      outbox rows and check it whenever legal mail is expected

## Step 6 — Runbook (2.5)

- [ ] Create `docs/runbook.md` with the five scenarios from TASKS (site down,
      OAuth broken, mails failing, migration failed, legal report) in the
      format: symptom → 3 diagnostic commands → fix → verification
- [ ] Fill in real resource names/commands from Steps 3–5
- [ ] **Test one restore** of the Neon DB (branch/restore feature) — checks the
      backup box; an untested backup does not exist

## Step 7 — Security & legal (2.6, parallel track)

- [ ] Send the four legal documents to a lawyer for fixed-fee review NOW
      (longest lead time in Phase 2)
- [ ] After staging is up: verify CORS/cookies/rate limits against real
      staging URLs — incl. the ADR 0012 behaviors: same-site login works on
      custom domains, `X-CSRF` enforcement, per-client-IP rate limiting
      through the ingress (`ForwardedHeaders__Enabled=true`)
- [ ] Triage Dependabot/CodeQL/Trivy to zero high/critical

## Exit gate check (from TASKS.md)

- [ ] Staging runs on Azure at ~0 EUR (credits)
- [ ] One full staging deploy + rollback executed
- [ ] Budget alerts armed
- [ ] Runbook exists and rehearsed once
- [ ] Legal texts submitted for review
