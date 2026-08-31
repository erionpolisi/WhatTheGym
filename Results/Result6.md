# WhatTheGym — Implementation Result 6

Date: 2026-09-01 · Executed: documentation review (focus: Azure go-live
plan), contradiction reconciliation across CONSTRAINTS/AGENTS/ADRs, TODO_NOW
refresh, CI publish gate
Status: **Complete — docs, contract files, ADRs, CI and Bicep are mutually
consistent; Bicep compiles with zero diagnostics**

## 1. Scope

Follow-up to Result5 (security hardening). Two passes:

1. Review of the Azure go-live plan (`docs/deployment-azure.md`, ADR 0008,
   TASKS Phase 2, `TODO_NOW.md`, `ci.yml`, Bicep parameters).
2. Full contradiction check CONSTRAINTS ↔ AGENTS ↔ ADR 0001–0012 ↔ code.

## 2. Go-live plan fixes (commits `8fd9bbb`, `7111f71`)

| Contradiction | Fix |
| --- | --- |
| deployment-azure.md demanded ACR + AcrPush + a "CI publish job" — TASKS 2.1 had decided **ghcr.io**, and ci.yml has no publish job | Rollout steps rewritten: ghcr.io, GitHub-OIDC federated identity, `deploy-staging.yml` / `deploy-production.yml` as explicit future workflows; registry row added to the topology table |
| `REGISTRY_TO_BE_CONFIGURED.azurecr.io` placeholders in both parameter files | `ghcr.io/OWNER_TO_BE_CONFIGURED/whatthegym-api:<tag>`; Bicep param description updated |
| Resource-group names `wtg-staging-rg`/`wtg-production-rg` (docs) vs `wtg-staging`/`wtg-prod` (TASKS/TODO_NOW) | Unified to `wtg-staging`/`wtg-prod` everywhere |
| ADR 0008 addendum (ghcr.io + cost table, TASKS 2.2) missing | Addendum written incl. estimated cost table (~3–9 EUR/month) |
| Unstated risks | Documented: SWA Free + Next.js SSR/ISR is preview-quality (fallback: frontend as second scale-to-zero container app, decide via ADR); `APPLICATIONINSIGHTS_CONNECTION_STRING` is set but no App Insights SDK is bundled (availability tests + Log Analytics are the monitoring story) |
| TODO_NOW stale after ADR 0012 | State table updated (all runtime config now wired in Bicep, KV secret fixed, ADR 0008 items done); Step 3 deploy command lists all required secure params (`googleClientSecret`, `analyticsHashSecret`, `resendApiKey`); explicit flag that **Google login cannot work on default hostnames** (SameSite=Lax needs same-site custom domains); stuck-outbox monitoring caveat; Step 7 verifies ADR 0012 behaviors on staging |

Bicep detail: the `#disable-next-line BCP318` suppression sat one line too
high; moved onto the offending line — template now compiles with **zero
diagnostics**.

## 3. Contract/ADR reconciliation (commits `dfdb933`, `c615120`)

| Contradiction | Resolution |
| --- | --- |
| CONSTRAINTS + AGENTS + ADR 0006: "store **exact/verbatim** notification text in the audit trail" vs the owner-approved token masking (ADR 0012) | Rule codified: exact text **with confidential status/appeal tokens masked as `***`** in the audit copy; ADR 0006 got a "superseded in part" amendment (also records the fast-track republish behavior) |
| CONSTRAINTS CI gate `… -> security scan -> publish` vs ci.yml without any publish step | Verify-only `dotnet publish` step added to the backend job (no push, no deploy — exactly the CONSTRAINTS wording); command verified locally in Release |
| CONSTRAINTS "local API `https://localhost:7001`" vs Docker Compose serving plain http | Clarified: Compose = `http://localhost:7001`, direct `dotnet run` = https (matches the http-client environments) |
| ADR 0003/0007 described the pre-0012 state without cross-references | ADR 0003 amendment (per-request session revalidation + CSRF check), ADR 0007 amendment (hold scopes resolved across review/user/case) |

## 4. Checked and consistent — no change needed

- ADR 0004 explicitly allows further migrations → `ActiveReviewUniqueIndex`
  conforms; CI migration checks cover it.
- ADR 0008 external-PostgreSQL default is the documented deviation that
  CONSTRAINTS' cost rule permits (vs. "PostgreSQL Flexible Server" wording).
- ADR 0012 ↔ ADR 0008 scale-to-zero decision consistently cross-referenced.
- CONSTRAINTS' coarse "Moderator/Admin" API-surface block vs Admin-only
  decisions in code: the roles section ("Admins manage all resources,
  Moderators remove reviews") is the finer rule; docs/api.md documents the
  split.
- Delivery promises verified present: `.env.example`, Dependabot, CodeQL,
  issue templates, Trivy, Conventional Commits.

## 5. Commits in this session

| Commit | Message |
| --- | --- |
| `c0c0fb4` | chore: ignore TypeScript incremental build cache (*.tsbuildinfo) |
| `8fd9bbb` | chore(infra): align image references with the ghcr.io registry decision |
| `7111f71` | docs: fix go-live plan contradictions, refresh TODO_NOW after hardening |
| `dfdb933` | ci: add verify-only publish step required by the CONSTRAINTS gate |
| `c615120` | docs: reconcile CONSTRAINTS/AGENTS/ADRs with the ADR 0012 hardening decisions |

## 6. Open items (unchanged from Result5 §5)

Product decisions still pending: purge of soft-deleted reviews (GDPR),
moderator access to reporter PII, token-in-URL for public case status,
security headers/CSP, appeal rate limit, search GET throttling. The go-live
plan itself is now executable top-to-bottom via `TODO_NOW.md`.
