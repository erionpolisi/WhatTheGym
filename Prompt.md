# Claude Fable Implementation Prompt

You are the senior software engineer and architect responsible for delivering
WhatTheGym as a complete, locally runnable, production-ready monolithic MVP.
Do not produce a prototype, a partial scaffold, or a design-only response.

Before changing files, read `AGENTS.md` and `CONSTRAINTS.md` completely.
They are binding. `CONSTRAINTS.md` is the full product and technical contract;
`AGENTS.md` defines repository-wide implementation behavior. Do not contradict
them or reintroduce explicitly excluded features.

## Objective

Deliver a working backend first: a .NET 8 Clean Architecture monolith with
PostgreSQL, EF Core migrations, Docker Compose, local seed data, Google-only
BFF authentication, reviews and scoring, search, moderation/legal-case flows,
privacy endpoints, mail outbox, tests, CI, documentation, and a slim Next.js
frontend integration. The result must build, test, and start locally before
any cloud deployment work is attempted.

## Execution Rules

1. Start with a concise plan that states phases, assumptions, and local
   validation commands. Then implement continuously phase by phase.
2. Make decisions only within the constraints. For an ambiguous reversible
   detail, choose a conservative default and record it as an ADR. Stop for a
   user decision only when it is irreversible or security/legal-critical.
3. After every phase, run the relevant build and tests, fix failures caused by
   your work, and report what was completed, validated, and still pending.
4. Do not ask for Azure, Google OAuth, Resend, DNS, or production secrets while
   implementing locally. Provide `.env.example`, safe development fallbacks,
   and documented placeholders instead.
5. Do not claim a task is finished without executable validation. Keep tests,
   migrations, API contracts, and documentation aligned with the implementation.

## Required Delivery Sequence

### Phase 1: Foundation

Create the monorepo folders and .NET solution with the four Clean Architecture
projects and three test projects. Configure nullable references, analyzers,
warnings as errors, Result handling, FluentValidation, Serilog correlation IDs,
ProblemDetails, `/api/v1`, Swagger, health checks, DI, EF Core PostgreSQL,
initial migrations, Docker Compose, `.env.example`, and local startup docs.

### Phase 2: Gym Catalogue and Search

Implement chains, gyms, amenities, stable SEO slugs, admin CRUD, status rules,
pagination, filters, PostgreSQL full-text/trigram search, summary endpoints,
and local-only deterministic official Vienna seed data. Implement only district,
score, area score, chain, and branch filtering; do not add prices, PostGIS, or
location radius.

### Phase 3: Reviews and Scoring

Implement authenticated, verified-Google reviews with direct 1-5 category
ratings, automatic publication, reversible soft deletion, revisions, content
validation, materialized score summaries, and rebuild command. Fully enforce
the two-area 50/50 aggregation and `scoreBasis` rules in `CONSTRAINTS.md`.
Add unit and integration tests for all invariants and public API behavior.

### Phase 4: Identity, Roles, and Moderation

Implement Google OIDC BFF with PKCE, cookie security, CORS allowlists,
rotating hashed refresh tokens, logout, user endpoints, configured first-admin
bootstrap, and role authorization. Add the limited Moderator review-removal
workflow and basic local anti-abuse safeguards.

### Phase 5: Legal, Privacy, and Notifications

Implement the single LegalCase report path, full audit trail, case status
machine, normal and fast-track visibility behavior, decisions, notifications,
appeals, token security, retention policies, legal holds, transparency report,
versioned legal documents, personal data export/deletion, and the tested
processing-activities record. Use a persistent outbox for Resend. Legal copy
must be marked as a draft for legal review.

### Phase 6: Frontend, Hardening, and Documentation

Implement the required thin Next.js frontend against the API: SEO-aware search,
detail, score display, reviews, Google login, report/contact forms, legal pages,
sitemap, robots, and structured data. Finish rate limits, analytics rules,
docs, ADRs, GitHub Actions, Dependabot, CodeQL, and Trivy configuration.

### Phase 7: Azure Preparation

After local end-to-end validation, add Bicep and deployment documentation for
local/staging/production Azure environments. Do not deploy or push images;
leave CI publish as a build/artifact verification step until a registry and
credentials are deliberately configured. Document the <= 10 EUR/month target
and actual expected costs.

## Completion Criteria

Finish only when the local Docker Compose application starts, migrations apply,
seed behavior is correct, all tests pass, Swagger exposes the required API,
the frontend can consume the locally running API, and the requested docs/CI/
Azure preparation are present. End with: completed work, exact local startup
commands, validation results, documented ADR defaults, and the next three
practical production steps.