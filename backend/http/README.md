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

Cookies (the BFF session) are stored automatically by Rider per host, so:
run a login request from `10-auth.http` once, then every authenticated
request just works. `client.global.set(...)` response handlers capture ids
and tokens (`reviewId`, `caseNumber`, `statusToken`, ...) so chained flows
run without copy-pasting.

CSRF note: authenticated state-changing requests must send `X-CSRF: 1` or a
JSON content type. JSON requests in this suite pass automatically; body-less
writes (refresh, logout, restore, close, publish, deletes, ...) carry the
header explicitly.

## Files

| File | Scope | Auth |
| --- | --- | --- |
| `00-health.http` | Liveness/readiness, Swagger | none |
| `10-auth.http` | Dev login, Google start, refresh, logout, `/me` (profile, export, deletion, own reviews) | creates session |
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

1. `10-auth.http`: dev-login as user → `30-reviews.http`: create review.
2. `30-reviews.http`: report the review (captures `caseNumber` + `statusToken`).
3. `40-legal-public.http`: check case status via token.
4. `10-auth.http`: dev-login as admin → `80-admin-legal.http`:
   classify → start-review → decide → close (or appeal in between).
5. `/transparenz` and the transparency-report endpoint reflect the outcome.

## Safety

- Requests marked with EXPECT comments are intentional error-path probes.
- Everything in `60`–`80` mutates real data: on staging/production only run
  them as a deliberate administrative action (they ARE the admin UI).
- Never paste production tokens into committed files.
