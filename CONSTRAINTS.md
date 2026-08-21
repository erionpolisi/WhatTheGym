# WhatTheGym Constraints

## Product and Scope

Build WhatTheGym, a production-ready monolithic fitness-gym review platform
for Vienna, Austria. The final domain is `whatthegym.at`; public content uses
German (`de-AT`). Code, comments, and commits use English.

The MVP lets users find gyms, inspect complete rating breakdowns, create
verified Google-account reviews, report reviews, and submit contact requests.
It must be locally runnable, tested, documented, and deployable later.

Do not build mobile apps, payments, affiliate/lead features, studio dashboards,
AI features, image upload/storage, microservices, brokers, Kubernetes, PostGIS,
geo search, Google Maps integration, or an admin frontend. Swagger is the admin
interface. Do not store, show, filter, or rank by membership prices.

## Architecture

- Monorepo: `backend/`, `frontend/`, `infrastructure/`, `docs/`.
- Backend: .NET 8 ASP.NET Core Web API, EF Core, normal PostgreSQL, REST,
  Swagger/OpenAPI. Use a single deployable monolith and one database.
- Clean Architecture: `Gym.Domain`, `Gym.Application`, `Gym.Infrastructure`,
  `Gym.Api`, plus domain, application, and PostgreSQL integration-test projects.
- Domain has no dependencies. Application contains commands, queries,
  validators, DTOs, and ports. Infrastructure owns EF Core and external
  adapters. API owns HTTP, middleware, and DI composition.
- Use CQRS-light, Result pattern for expected errors, FluentValidation,
  structured Serilog logging with correlation IDs, RFC 7807 ProblemDetails,
  rate limiting on writes, `/api/v1`, `/health/live`, and `/health/ready`.
- Enable nullable references, warnings as errors, analyzers, and `.editorconfig`.
- Search uses PostgreSQL full-text search, GIN indexes, and trigram matching
  behind `ISearchIndex`/`IGymSearchQuery`. No external search service.

## Local First, Azure Prepared

- Local is the mandatory first target: Docker Compose runs API, PostgreSQL, and
  optional pgAdmin without cloud credentials. Include `.env.example`, migrations,
  seed support, and an onboarding guide.
- Maintain local, staging, and production configuration. Local frontend/API are
  `http://localhost:3000` and `https://localhost:7001`; staging is
  `staging.whatthegym.at` and `api-staging.whatthegym.at`; production is
  `whatthegym.at` and `api.whatthegym.at`.
- Prepare Bicep and deployment documentation for Azure Static Web Apps,
  API hosting, PostgreSQL Flexible Server, Key Vault, and Application Insights.
  Do not deploy or require Azure, DNS, Google OAuth, Resend, or registry secrets
  before local integration works.
- Target no more than 10 EUR/month in ongoing production cost. Document
  deviations and alternatives in an ADR.

## Authentication and Roles

- Google-only login via ASP.NET Core BFF, Google Authorization Code Flow with
  PKCE, and `Secure`, `HttpOnly`, appropriate `SameSite` cookies.
- Never send provider/application tokens to Next.js and never use browser
  storage for tokens. Enforce the environment-specific CORS allowlist.
- Rotate, hash, and revoke refresh tokens server-side; detect reuse.
- Roles are `User`, `Moderator`, and `Admin`. Admins manage roles and all
  resources. Moderators remove reviews. Bootstrap the first admin only by a
  configured verified Google email, never seed data.

## Domain Rules

- GymChain, Gym, Amenity, User, Review, ReviewRevision, LegalCase,
  LegalCaseAppeal, LegalCaseEvent, GymRatingSummary, ContactRequest,
  AnalyticsEvent, LegalDocument, and persistent email Outbox are required.
- Gyms have stable unique SEO slugs, Vienna district, optional official opening
  hours, chain, amenities, website, phone, status, and timestamps. Use ISO
  country code `AT`, UTC timestamps, and `Europe/Vienna` for date-based rules.
- Reviews are never anonymous. A Google `email_verified=true` account gets
  "Verifiziert ueber Google"; this does not prove membership.
- A review requires at least one direct 1-5 rating. Membership ratings are
  PriceValue, ContractTerms, Billing, and CancellationExperience. Studio
  ratings are Equipment, Cleanliness, Staff, Crowding, ChangingRoom, Showers,
  and Atmosphere. Free text is optional, bounded, and sanitized.
- Publish reviews automatically. A review may be reversible soft deleted or
  temporarily under review; neither is public or score-relevant. Preserve
  revisions according to retention and legal holds.
- Aggregate available categories only. A gym total is 50/50 between membership
  and studio when both areas have data; otherwise total equals the available
  area and exposes `scoreBasis`. Include total, both areas, every category,
  and underlying review counts in score responses. Missing data is `null`,
  never zero. Materialize summary updates and provide a rebuild command.
- Search/filter only by district, total score, area score, chain, and branch.
  No price or location-radius behavior in this MVP.
- Only Admins create gyms. Public gym/data suggestions use `ContactRequest`.
  Seed real official Vienna gym data and approximately 50 studios; seed demo
  reviews/cases only in local/development, never staging or production.

## Reports, Legal, Privacy, and Email

- One public review-report endpoint creates a `LegalCase`; do not create
  `ReviewReport`, partial redactions, `ReviewRedaction`, or related endpoints.
- Legal decisions are `KeepOnline` or `FullyRemoved`; removal changes Review to
  `RemovedLegal`. Keep content online while a normal report is reviewed. Only
  an explicitly classified obviously-illegal fast-track case may hide it first.
- Cases require human-readable numbers, status transitions, rationale, parties,
  notification snapshots, appeal access, and append-only immutable events.
  Appeals remain accessible at least six months after the original decision.
- Retention is configurable and legal holds pause deletion. Retain case audit
  events seven years after closure and review revisions three years after review
  removal unless a valid hold/obligation requires longer retention.
- Treat every value logically linkable to a person as personal data. Generate
  and test a processing-activities record, expose legal document version APIs,
  data export, account deletion/anonymization, and transparency-report APIs.
  Mark all legal text `ENTWURF - anwaltlich pruefen lassen`.
- Legal/contact mail uses Resend through a persistent transactional outbox with
  retry. Store exact legal notification text in the case audit trail.
- Apply rate limiting, honeypots, and server-side abuse/spam checks to public
  forms. No CAPTCHA service. Analytics is PII-free, allowlisted, no IP storage,
  no fingerprinting, and uses short-lived rotating hashed session buckets.

## Required API Surface

- Public: gym search/detail/reviews/summary, amenities, chains, contact requests,
  analytics events, legal documents and processing activities, transparency
  report, review legal report, case status with token, and tokenized appeals.
- Authenticated: Google start/callback/refresh/logout, `/me`, profile update,
  personal data export, account deletion, create/edit/delete own reviews.
- Moderator/Admin: moderation queues, review removal, legal case review and
  decision, case event access, case export, gym/chain/amenity management, role
  administration, and versioned legal-document CRUD. Use stable pagination,
  sorting, ETags, and caching where appropriate.

## Quality and Delivery

- Provide EF Core migrations, deterministic local development seeding, Swagger
  descriptions/examples, Docker Compose, GitHub Actions, Dependabot, CodeQL,
  Trivy when images are built, issue templates, Conventional Commits, and
  `main` plus `feature/` branching.
- CI: `restore -> build -> unit tests -> integration tests -> migration/schema
  check -> security scan -> publish`. Validate pending migrations and apply
  migrations to fresh normal PostgreSQL. High/critical security findings block;
  low findings warn. Publish only builds/verifies artifacts/images until an
  Azure registry is configured; it does not push or deploy.
- Create architecture, domain, API, scoring, legal, processing-activities,
  onboarding, SEO, and ADR documentation. The Next.js frontend is mandatory
  but secondary: SEO-aware public pages, Google login, search, gym detail,
  review form/list, report form, legal pages from backend, SSR/SSG, sitemap,
  robots, and schema.org markup.