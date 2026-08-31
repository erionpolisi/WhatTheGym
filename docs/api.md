# API overview

Swagger (`/swagger`) is the authoritative, always-on API reference and serves
as the admin interface for the MVP. All routes live under `/api/v1`.
Errors are RFC 7807 ProblemDetails with a stable `code` extension.

## Public (anonymous)

| Method | Route | Notes |
| ------ | ----- | ----- |
| GET | `/gyms` | Search: `term`, `district`, `chain`, `minTotalScore`, `minMembershipScore`, `minStudioScore`, `sort` (`score`\|`name`\|`newest`\|relevance), `page`, `pageSize` |
| GET | `/gyms/{slug}` | Detail incl. chain, amenities, opening hours, score summary; ETag + cache headers |
| GET | `/gyms/{slug}/summary` | Score summary (see docs/scoring.md) |
| GET | `/gyms/{slug}/reviews` | Published reviews, paged, never anonymous |
| GET | `/chains`, `/amenities` | Catalogue lookups |
| POST | `/reviews/{id}/report` | Review report → LegalCase; honeypot `website`; rate limited |
| GET | `/legal/cases/{caseNumber}/status?token=` | Tokenized case status |
| POST | `/legal/cases/{caseNumber}/appeal` | Tokenized appeal (≥ 6 months) |
| GET | `/legal/documents/{type}` | Active legal document (`imprint`, `privacyPolicy`, `termsOfUse`); ETag |
| GET | `/legal/documents/{type}/versions` | Version history |
| GET | `/legal/processing-activities` | GDPR Art. 30 record |
| GET | `/legal/transparency-report?year=` | Aggregate counts |
| POST | `/contact-requests` | Contact/suggestion/correction form; honeypot; rate limited |
| POST | `/analytics/events` | Allowlisted, PII-free events; rate limited |

## Authenticated (cookie session)

State-changing requests (`POST`/`PUT`/`PATCH`/`DELETE`) from an authenticated
session must either send the custom header `X-CSRF: 1` or a JSON content type
(defense-in-depth CSRF check; cross-site HTML forms can produce neither).
Body-less calls such as `POST /auth/refresh` therefore need the header.
Swagger UI injects it automatically; anonymous requests are unaffected.

| Method | Route | Notes |
| ------ | ----- | ----- |
| GET | `/auth/google/start?returnUrl=` | Google Authorization Code Flow with PKCE (503 when unconfigured) |
| POST | `/auth/refresh` | Rotates the hashed server-side refresh token |
| POST | `/auth/logout` | Revokes refresh token, clears session |
| POST | `/auth/dev-login` | Development-only fallback login |
| GET/PUT | `/me` | Profile |
| GET | `/me/reviews` | Own reviews incl. status |
| GET | `/me/export` | Personal data export (JSON) |
| DELETE | `/me` | Account deletion/anonymization |
| POST | `/gyms/{slug}/reviews` | Create review (verified Google account) |
| PUT/DELETE | `/reviews/{id}` | Edit (same content rules as create, archives revision; only published reviews) / soft delete own review |

## Moderator (`Moderator` or `Admin` role)

| Method | Route |
| ------ | ----- |
| GET | `/moderation/reviews?status=` |
| POST | `/moderation/reviews/{id}/remove` (reason required, reversible) |
| GET | `/admin/legal-cases`, `/admin/legal-cases/{id}` |

## Admin only

| Method | Route |
| ------ | ----- |
| POST | `/moderation/reviews/{id}/restore` |
| GET/POST/PUT/PATCH | `/admin/gyms...` (list incl. drafts, create, update, status) |
| POST/PUT/DELETE | `/admin/chains...`, `/admin/amenities...` |
| GET/PUT | `/admin/users`, `/admin/users/{id}/role` |
| GET/PUT | `/admin/contact-requests...` |
| POST | `/admin/legal-cases/{id}/classify\|start-review\|decide\|close` |
| POST | `/admin/legal-cases/appeals/{id}/decide` |
| GET | `/admin/legal-cases/{id}/export` (JSON download) |
| POST | `/admin/legal-holds`, `/admin/legal-holds/{id}/release` |
| POST | `/admin/legal-documents`, `/admin/legal-documents/{id}/publish` |
| POST | `/admin/summaries/rebuild` |

Pagination is stable (`page`, `pageSize` ≤ 100, ordered by deterministic keys);
gym detail and legal documents support `ETag`/`If-None-Match` with
`Cache-Control`.
