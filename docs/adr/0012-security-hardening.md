# ADR 0012: Security hardening after the 2026-08 code review

Status: accepted — 2026-09-01

## Context

A full code review (see `Results/Result5.md`) found contradictions and
vulnerabilities across auth, the legal audit trail, retention, and the
prepared Azure infrastructure. The fixes below required decisions where
product rules were in tension with each other.

## Decisions

### 1. Confidential tokens are masked in audit-trail notification snapshots

CONSTRAINTS requires the exact legal notification text in the append-only
audit trail, while tokens must only be stored hashed. Storing mail bodies
verbatim leaked usable status/appeal tokens to everyone with case access
(Moderator policy). Resolution: the audit copy of a notification is the exact
text with the confidential token replaced by `***` (`LegalLinks.MaskedToken`);
the outbox mail carries the real link. The legally relevant wording is
preserved; secrets are not.

### 2. Cookie sessions are revalidated against the user store on every request

Role changes, account deletion, and server-side revocation previously only
took effect after cookie expiry (up to 60 minutes). `OnValidatePrincipal` now
loads the user per request: inactive/deleted users are rejected and signed
out; stale claims (role, email, verification) are replaced and the cookie
renewed. One extra DB query per authenticated request is acceptable for the
MVP traffic profile.

### 3. Forwarded headers are honored behind a trusted ingress only

`ForwardedHeaders:Enabled=true` (set by Bicep for Container Apps) makes the
app honor `X-Forwarded-For`/`X-Forwarded-Proto` with cleared
`KnownNetworks`/`KnownProxies`. This is only safe because the app is reachable
exclusively through the platform ingress; the flag stays `false` for local
Docker. Without it, rate limiting collapsed to a single bucket (proxy IP) and
OIDC redirect URIs degraded to `http://`.

### 4. CSRF: keep `SameSite=Lax`, add a defense-in-depth request check

Primary CSRF defense remains `SameSite=Lax` cookies, which requires same-site
deployment of frontend and API (documented in `docs/deployment-azure.md`).
Additionally, `CsrfHeaderMiddleware` rejects authenticated state-changing
requests that neither carry `X-CSRF: 1` nor a JSON content type — neither can
be produced by a cross-site HTML form, and cross-site fetch with JSON triggers
a CORS preflight. Swagger UI injects the header via request interceptor.

### 5. One active review per user and gym is enforced by the database

The application-level check is racy. A filtered unique index
(`IX_Reviews_UserId_GymId_Active`, `Status IN ('Published','UnderReview')`)
guarantees the invariant; the unit of work translates PostgreSQL unique
violations into `UniqueConstraintViolationException`, mapped to HTTP 409.

### 6. Review edits follow the same content rules as creation

`UpdateOwnReviewCommand` now runs the full validator (including the link-spam
check that previously only applied to creation), and `Review.Edit` only
accepts `Published` reviews — soft-deleted content is frozen until restored.

### 7. Retention honors user-scoped legal holds

The sweeper previously ignored `LegalHold.UserId`. Revision purges now skip
reviews protected by a review- or user-scoped hold; case purges skip cases
protected by a hold on the case, the reported review, or the review author.

### 8. Fast-track misclassification is reversible

Reclassifying a fast-track case to `Normal` republishes the hidden review
(audited as `ContentRestored`), unless another open fast-track case for the
same review exists. This restores the rule "content stays online until a
documented decision" after an operator error.

### 9. Scale-to-zero stays despite background services

Cost cap (ADR 0008) wins for the MVP: `minReplicas: 0` remains. Outbox mails
and retention sweeps only run while an instance is warm; the limitation is
documented in `docs/deployment-azure.md` and must be revisited (minReplicas 1
or a scheduled trigger) if legal notification latency becomes a compliance
concern.

## Consequences

- Staging/production Bicep now fails fast on missing auth/mail/analytics
  configuration instead of booting a silently broken environment (the
  Key Vault secret for `deployPostgres=true` is also fixed).
- One additional DB read per authenticated request (decision 2).
- REST clients must send `X-CSRF: 1` on body-less authenticated writes
  (documented in `docs/api.md` and `backend/http/`).
- Existing databases with duplicate active reviews would block the new
  migration; none exist (greenfield).
