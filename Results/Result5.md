# WhatTheGym — Implementation Result 5

Date: 2026-09-01 · Executed: security-hardening fixes from the full code
review (2026-08-31), including tests, documentation, and infrastructure
Status: **Complete — 537 domain + 427 application + 308 integration tests
green, frontend build green, Bicep compiles, Docker-Compose smoke test green**

## 1. Owner decisions

1. **Bicep stays scale-to-zero** (`minReplicas: 0`, cost cap): the limitation
   (outbox mails / retention sweeps only run while an instance is warm) is
   documented in `docs/deployment-azure.md` and ADR 0012.
2. **CSRF strategy**: keep `SameSite=Lax` as primary defense (same-site
   deployment documented as mandatory) plus a defense-in-depth check —
   authenticated writes need `X-CSRF: 1` or a JSON content type.
3. **Session revalidation on every request** (1 DB read per authenticated
   request accepted for MVP traffic).

## 2. Fixes implemented (review finding → change)

| # | Finding (Review) | Fix |
| - | ---------------- | --- |
| 1 | Plaintext status/appeal tokens in `LegalCaseEvents` mail-body snapshots, readable by moderators | Audit copies mask tokens as `***` (`LegalLinks.MaskedToken`); mails keep real links. `ReportReviewCommandHandler`, `DecideCaseCommandHandler` |
| 2 | Cookie sessions never revalidated (role demotion/deletion ineffective until expiry) | `OnValidatePrincipal`: per-request user-store check, reject inactive, refresh stale claims (`Program.cs`) |
| 3 | No forwarded-headers handling (rate limiter on proxy IP, OIDC redirect `http://`) | `ForwardedHeaders:Enabled` config + `app.UseForwardedHeaders()`; enabled via Bicep env for Container Apps |
| 5 | No explicit CSRF defense | `CsrfHeaderMiddleware` (header or JSON content type), Swagger request interceptor, frontend/http-suite updated |
| 6 | Bicep: missing Google/Resend/PublicBaseUrl/HashSecret/Bootstrap config; KV secret broken for `deployPostgres=true`; scale-to-zero conflict | New required/secure params wired as env+secrets; KV secret always created (managed conn string or external); tradeoff documented |
| 7 | One-active-review-per-user+gym only app-level (racy) | Filtered unique index `IX_Reviews_UserId_GymId_Active` (migration `ActiveReviewUniqueIndex`); unique violations → `UniqueConstraintViolationException` → HTTP 409 |
| 8 | Link-spam check only on create, not edit | `UpdateOwnReviewCommandValidator` (full create rules incl. link check) |
| 9 | `RetentionSweeper` ignored `LegalHold.UserId` | Revision purge skips review-/user-scoped holds; case purge skips case/review/author holds |
| 11 | FastTrack→Normal reclassification left review hidden | Auto-release + `ContentRestored` audit event (unless another open fast-track case exists) |
| 12 | Soft-deleted reviews editable by author | `Review.Edit` only accepts `Published` |
| 15 | Refresh rotation: inactive user kept token/cookie | `RotateAsync` revokes token + clears cookie |
| 18 | Chain website without URL validation (`javascript:` possible) | `http(s)`-only validation in create/update chain handlers |

## 3. Tests

- New: `SecurityHardeningTests` (application, 7 tests — token masking,
  fast-track release, edit validation), `Edit_is_rejected_for_soft_deleted_reviews`
  (domain), `SecurityHardeningApiTests` (integration, 9 tests — CSRF
  negative/positive, live role change/deletion on existing session, DB unique
  index, end-to-end token masking incl. working reporter link, fast-track
  republish, user-scoped hold in sweeper, chain URL).
- Full runs: 537 domain, 427 application, 308 integration (Testcontainers) —
  all green. Frontend `tsc`/`next build` green. `main.bicep` compiles
  (single documented `#disable-next-line BCP318`).
- Live smoke test via `docker compose up`: migration applied (index verified
  in PostgreSQL), dev-login, CSRF 403/200 behavior, `/me`, public search.

## 4. Documentation

- **ADR 0012** (`docs/adr/0012-security-hardening.md`): all nine decisions
  with rationale.
- `docs/api.md` (CSRF contract), `docs/architecture.md` (hardening section),
  `docs/legal.md` (token masking, hold scopes, fast-track reversal),
  `docs/deployment-azure.md` (Bicep params, same-site requirement,
  scale-to-zero limitation), `backend/http/README.md` (CSRF note).

## 5. Explicitly not changed (open items from the review)

- Soft-deleted reviews are never purged (GDPR tension, review finding #10) —
  needs a product/retention decision.
- Moderators can read reporter name/email in case details (finding #14) —
  CONSTRAINTS grants moderators case access; worst impact removed by token
  masking.
- Token-in-URL for public case status (finding #16), security headers/CSP
  (#19), mail to unverified addresses (#20), unlimited appeals per token
  (#21), unthrottled search GETs (#22) — candidates for a follow-up.
