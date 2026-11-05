# HTTP Client Suite (JetBrains Rider)

Manual/exploratory request collection for the WhatTheGym API using the
[JetBrains HTTP Client](https://www.jetbrains.com/help/rider/Http_client_in__product__code_editor.html)
built into Rider. It complements — never replaces — the automated unit and
integration tests: use it to explore, reproduce bugs, administer data
(Swagger-equivalent), and smoke-test environments.

## Usage

1. Open any `.http` file in Rider.
2. Pick the environment in the editor toolbar: `local-docker` (default,
   `docker compose up`), `local-dotnet` (`dotnet run`, HTTPS), `staging`,
   `production`.
3. Click the gutter "run" icon next to a request.

Google login is browser-based. Run authenticated exploratory and admin requests
through Swagger in the same browser session. The Rider suite remains useful for
public requests and for authenticated debugging when its local cookie jar has
been populated deliberately; never copy staging or production cookies into
committed files. `client.global.set(...)` response handlers capture ids and
public-flow tokens (`reviewId`, `caseNumber`, `statusToken`, ...).

CSRF note: authenticated state-changing requests must send `X-CSRF: 1` or a
JSON content type. JSON requests in this suite pass automatically; body-less
writes (refresh, logout, restore, close, publish, deletes, ...) carry the
header explicitly.

## Files

| File | Scope | Auth |
| --- | --- | --- |
| `00-health.http` | Liveness/readiness, Swagger | none |
| `10-auth.http` | Google start, refresh, logout, `/me` (profile, export, deletion, own reviews) | browser session |
| `20-catalog-public.http` | Gym search incl. every filter, detail, summary, review list, chains, amenities, ETag/404 variants | none |
| `30-reviews.http` | Create/edit/delete reviews, every validation variant, review report (creates LegalCase), honeypot, rate-limit probe | user session |
| `40-legal-public.http` | Legal documents + versions, processing activities, transparency report, tokenized case status and appeal | none (tokens) |
| `50-contact-analytics.http` | Contact requests (all types, honeypot, validation), analytics events (allowlist, rate limit) | none |
| `60-moderation.http` | Moderation queue, remove/restore, user roles, contact-request admin | Moderator/Admin |
| `70-admin-catalog.http` | Gym/chain/amenity CRUD, status transitions, summary rebuild | Admin |
| `80-admin-legal.http` | Full LegalCase state machine, appeals, legal holds, legal-document versioning | Moderator/Admin |

## Environments and secrets

- `http-client.env.json` — public values (hosts, seeded slug, dev emails).
  Committed.
- `http-client.private.env.json` — secrets (real case tokens etc.).
  **Gitignored.** Create it from `http-client.private.env.json.example`.

## Full legal-flow walkthrough (local)

1. Sign in with Google and create a review through the website or Swagger.
2. `30-reviews.http`: report the review (captures `caseNumber` + `statusToken`).
3. `40-legal-public.http`: check case status via token.
4. Sign in as the configured bootstrap Admin and use Swagger or
  `80-admin-legal.http` with an explicitly populated local cookie jar:
   classify → start-review → decide → close (or appeal in between).
5. `/transparenz` and the transparency-report endpoint reflect the outcome.

## Safety

- Requests marked with EXPECT comments are intentional error-path probes.
- Everything in `60`–`80` mutates real data: on staging/production only run
  them as a deliberate administrative action (they ARE the admin UI).
- Never paste production tokens into committed files.
