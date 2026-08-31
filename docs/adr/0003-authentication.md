# ADR 0003: Google BFF authentication, custom refresh tokens, dev-login fallback

Status: accepted — 2026-08-21

## Context

Login is Google-only via a BFF with Authorization Code Flow + PKCE. Tokens must
never reach the browser beyond HttpOnly cookies, refresh tokens must rotate and
be revocable server-side, and local development must work without Google
credentials.

## Decision

- The ASP.NET Core OpenIdConnect handler (authority `accounts.google.com`,
  `response_type=code`, PKCE, `SaveTokens=false`) performs the Google flow only
  when a client id/secret are configured. On ticket receipt the user is
  upserted and the principal is replaced with application claims (user id,
  role, email_verified).
- Sessions are short-lived cookie sessions (60 min, `HttpOnly`, `SameSite=Lax`,
  `Secure` on HTTPS). Refresh uses our own opaque 256-bit tokens stored as
  SHA-256 hashes with expiry, rotation chains (`ReplacedByTokenHash`) and
  reuse detection that revokes the whole token family.
- Development fallback: `POST /api/v1/auth/dev-login` exists only when the
  environment is Development **and** `Auth:EnableDevLogin=true`. It mimics the
  Google upsert (subject `dev:<email>`), enabling the full flow locally with
  zero secrets. It is unreachable in staging/production by construction.
- First-admin bootstrap: a configured verified Google email becomes Admin on
  login while no admin exists — never via seed data.

## Consequences

- No provider tokens are stored anywhere; browser storage holds nothing.
- Logout and account deletion revoke all refresh tokens.
- The dev-login is an accepted, documented local-only risk; integration tests
  cover login, refresh rotation, logout revocation and role enforcement.

## Amendment (2026-08-31): cookie Secure policy hardened

Both the session cookie and the refresh cookie previously derived their
`Secure` flag from the inbound request scheme (`SameAsRequest` /
`Request.IsHttps`). Behind a TLS-terminating proxy with missing forwarded
headers this could emit non-Secure auth cookies. Decision: outside the
Development environment the `Secure` flag is now unconditional
(`CookieSecurePolicy.Always` and the equivalent for the refresh cookie);
Development keeps scheme-dependent behavior so plain-HTTP Docker login works.

## Amendment (2026-09-01): session hardening (ADR 0012)

Cookie sessions are revalidated against the user store on every request
(role changes, deletion, and revocation take effect immediately), and
authenticated state-changing requests require the `X-CSRF` header or a JSON
content type. Details and rationale in ADR 0012.
