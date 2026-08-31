# TODO_NOW.md — Continue Phase 2 (step by step)

Snapshot 2026-08-31 of where Phase 2 stands and exactly what to do next.
Companion to TASKS.md Phase 2; delete this file once the exit gate is met.

## Current state (verified in repo)

| Phase 2 item | State |
| --- | --- |
| Bicep templates | ✅ Prepared, never deployed — `infrastructure/azure/main.bicep` has Container Apps (scale-to-zero, 0.25 vCPU/0.5Gi), SWA Free, Key Vault, capped Log Analytics |
| Parameters | ⚠️ `parameters.staging.json` still has `REGISTRY_TO_BE_CONFIGURED.azurecr.io` placeholder — conflicts with the ghcr.io decision in TASKS |
| CI | ✅ `.github/workflows/ci.yml` builds/tests/scans, but `publish` job explicitly does **no push, no deploy** |
| Deploy workflow | ❌ Doesn't exist (no OIDC, no ghcr push, no `az containerapp update`) |
| Runbook | ❌ `docs/runbook.md` doesn't exist |
| ADR 0008 | ⚠️ Needs addendum: ghcr.io instead of ACR, filled cost table |
| Registry auth in Bicep | ⚠️ Bicep has no `registries` config — needed if the ghcr package is private |

Flag: Phase 1's exit gate isn't fully met (domain not registered, Google
OAuth/Resend accounts missing). Fine for most of Phase 2, but **domain
registration blocks** DNS-dependent steps (OAuth redirect URIs, custom domains).

---

## Step 0 — Unblock from Phase 1.5 (do first, has lead time)

- [ ] Register `whatthegym.at` (easyname/World4You/INWX, ~15–30 EUR/yr).
      DNS propagation and OAuth consent verification both take time — start now.
- [ ] Create Google Cloud project + OAuth consent screen (external) + OAuth
      client (free, no Azure dependency)
- [ ] Create Resend account (free tier; domain verification needs Step 0.1 DNS)

## Step 1 — Azure foundation (2.1, ~1 hour, 0 EUR)

- [ ] Activate **Azure for Students**; put credit expiry date in calendar
- [ ] Install/verify `az` CLI, `az login`
- [ ] Create resource groups:
      ```
      az group create -n wtg-staging -l westeurope
      az group create -n wtg-prod -l westeurope
      ```
      Note: `docs/deployment-azure.md` says `wtg-staging-rg`, TASKS says
      `wtg-staging` — pick one and fix the doc.
- [ ] Budget alerts in Cost Management: 1 / 5 / 10 EUR forecast on the subscription
- [ ] Sign up for **Neon** (or Supabase) free tier, EU region → create staging
      database → keep connection string for Step 3

## Step 2 — Repo changes for ghcr.io + deploy pipeline (main coding work)

- [ ] Update `infrastructure/azure/parameters.staging.json` and
      `parameters.production.json`: `apiImage` → `ghcr.io/<your-user>/whatthegym-api:<tag>`
- [ ] If ghcr package will be private (recommended): add `registries` block +
      PAT secret to the Container App in Bicep; if public, no change needed
- [ ] Set up **GitHub OIDC → Azure** federated identity (no static secrets):
      `az ad app create` + service principal + federated credential for
      `repo:<you>/WhatTheGym:ref:refs/heads/main` (and one for the prod
      workflow/tag), Contributor on the two RGs
- [ ] New workflow `deploy-staging.yml`: on `main` push, after CI →
      `docker build` → push to ghcr.io with `GITHUB_TOKEN` →
      `az containerapp update -n wtg-staging-api -g wtg-staging --image ghcr.io/...:<sha>`
- [ ] New workflow `deploy-production.yml`: `workflow_dispatch` (input: image
      tag) or `v*` tag trigger only — never automatic
- [ ] ADR 0008 addendum: ghcr.io decision + filled cost table (TASKS 2.2)

## Step 3 — First staging deployment (validates the never-executed Bicep)

- [ ] Deploy:
      ```
      az deployment group create -g wtg-staging -f infrastructure/azure/main.bicep `
        -p '@infrastructure/azure/parameters.staging.json' `
        -p externalPostgresConnectionString='<Neon connection string>'
      ```
      Expect iteration: first-run Bicep almost always surfaces small issues
      (Key Vault name `wtg-staging-kv` must be globally unique; role-assignment
      propagation timing; SWA region).
- [ ] Push a first image manually to ghcr.io so the Container App has something
      to pull; verify `/health/live` and `/health/ready` on the outputted `apiUrl`
- [ ] Verify migrations ran + catalog seeded, no demo data
      (`Seed__SeedDemoData=false` is already in the Bicep)
- [ ] Add remaining secrets (Resend key, Google OAuth, `Auth__BootstrapAdminEmail`,
      `Analytics__HashSecret`) to Key Vault and wire them as Container App
      secrets — Bicep currently only wires the Postgres connection string, so
      this needs a small Bicep extension
- [ ] Once DNS exists: point `api-staging.whatthegym.at` + `staging.whatthegym.at`,
      add custom domains/managed certs, test real Google login

## Step 4 — Rehearse rollback (2.3, while nothing matters)

- [ ] Deploy image tag A, then tag B, then roll back:
      `az containerapp update --image ...:<tagA>`
      (or `az containerapp revision activate`)
- [ ] Write the exact commands down — first runbook entry

## Step 5 — Monitoring (2.4, ~30 min)

- [ ] App Insights availability test on `/health/ready` + alert rule → your email
- [ ] Write 3–4 SQL queries (page views/day, reviews/day, top gyms, stuck
      outbox mails) — these go in the runbook

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
      staging URLs
- [ ] Triage Dependabot/CodeQL/Trivy to zero high/critical

## Exit gate check (from TASKS.md)

- [ ] Staging runs on Azure at ~0 EUR (credits)
- [ ] One full staging deploy + rollback executed
- [ ] Budget alerts armed
- [ ] Runbook exists and rehearsed once
- [ ] Legal texts submitted for review
