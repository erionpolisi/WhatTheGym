# WhatTheGym — Implementation Result 2

Date: 2026-08-31 · Executed: UI/UX redesign, go-live roadmap (TASKS.md),
Rider HTTP client suite, documentation updates
Status: **Complete — all goals implemented and locally validated**

## 1. Summary

Building on the complete MVP (Result 1), this iteration delivered:

1. **Modern UI redesign** of the Next.js frontend: dark minimalist design
   system with a single warm-orange accent, oversized typography, and large,
   prominent rating scores (giant total numeral + category fill bars).
2. **TASKS.md** at the repository root: the complete phased roadmap from
   local finishing through Azure cost-capped deployment, social-media
   validation, safe go-live, and monetization (ads + B2B provision models).
3. **Rider HTTP client suite** (`backend/http/`): every API endpoint incl.
   error/honeypot/rate-limit variants, environment switching, token capture.
4. **Documentation**: ADR 0010 (design system), README/onboarding updates.

No backend code changed; product rules (CONSTRAINTS.md) untouched.

## 2. UI redesign (Goal 1)

Trend selection from the provided 9-trend list (decision recorded in ADR 0010):

| Trend | Verdict | Usage |
| --- | --- | --- |
| #1 Barely There UI | **adopted** | foundation: dark near-black surfaces, whitespace, thin borders, restrained palette |
| #2 Controlled Maximalism | **adopted (typography only)** | huge display headlines, giant score numerals `clamp(4rem…6.5rem)` |
| #4 Grade-School Colors | **adopted (accent only)** | single warm orange `#ff5c1f` for scores/CTAs/focus |
| #6 Fancy Animations | **light touch** | CSS-only micro-interactions, `prefers-reduced-motion` guarded |
| #5 Spaceship Manual | **detail only** | `tabular-nums` for all score figures |
| #3, #7, #8, #9 | rejected | trust/professionalism/a11y ("not colorful" requirement) |

User-confirmed choices: dark theme, warm orange accent.

Implementation (KISS/SOLID):

- `frontend/app/globals.css` fully rewritten as a token-based design system
  (colors, type scale, radius, components) — plain CSS custom properties,
  **no Tailwind, no runtime dependencies**.
- Fonts: `Space Grotesk Variable` (display) + `Inter Variable` (body) via
  self-hosted Fontsource packages — zero external requests (GDPR/proxy-safe).
- `components/Scores.tsx` redesigned: `ScorePill` (big numeral + small /5),
  clickable `GymCard`, `ScoreBreakdown` with score hero (giant total, area
  split) and per-category horizontal fill bars with rating counts; missing
  data renders as "–"/"keine Daten", never zero.
- Pages restructured: home hero (eyebrow, oversized headline, category
  chips), studios grid layout + framed filter bar, detail-page header,
  amenity chips, semantic pagination; layout gets skip-link, sticky
  backdrop-blur header, footer nav landmark.
- Responsive mobile-first (verified at 390 px), WCAG AA contrast (accent on
  background ≈ 6.3:1), visible focus rings, aria-labels on score bars.

## 3. Validation results (all executed locally)

| Check | Result |
| ----- | ------ |
| `dotnet build WhatTheGym.sln` (warnings as errors) | 0 errors, 0 warnings |
| Unit tests `Gym.Domain.Tests` | **40/40 passed** |
| Unit tests `Gym.Application.Tests` | **22/22 passed** |
| Integration tests | not re-run (Docker Desktop not running this session); backend code untouched — last known green 33/33 (Result 1) |
| Frontend `npm run build` | ✓ Compiled successfully, 13/13 pages (API-offline fetch fallbacks behaved as designed) |
| Visual verification (headless Edge + puppeteer-core, emulated 390 px mobile + 1280 px desktop) | hero, cards, filter bar, forms, score hero + bars verified; **0 overflowing elements** (scrollWidth = viewport) |
| Bug found & fixed during visual check | score-bar `track`/`fill` spans needed `display: block` — bars now render |

## 4. Rider HTTP client (Validation request 2)

Yes — fully supported via JetBrains HTTP Client. Delivered `backend/http/`:

- `http-client.env.json` (committed): `local-docker`, `local-dotnet`,
  `staging`, `production` hosts + safe defaults;
  `http-client.private.env.json` gitignored (example template provided).
- 9 request files covering **every endpoint variant**: health/Swagger, auth
  (dev-login roles, Google start, refresh rotation, logout, `/me` CRUD,
  export, deletion), catalogue (all search filters, ETag/404 variants),
  reviews (create full/minimal/invalid/spam/unauthenticated, edit, delete,
  report + honeypot + rate-limit probe), public legal (documents, versions,
  processing activities, transparency, tokenized status/appeal), contact +
  analytics (all types, honeypot, allowlist rejection), moderation (queue,
  remove/restore, roles, contact admin), admin catalogue (gym/chain/amenity
  CRUD, status transitions, summary rebuild), admin legal (full case state
  machine, appeals, holds, document versioning).
- Every request documents **WHEN / WHY / WHERE / AUTH** and expected error
  codes; `client.global.set` handlers chain ids/tokens (reviewId,
  caseNumber, statusToken, …) so full flows run without copy-paste.
- Session cookies are handled automatically by Rider (BFF pattern works).

## 5. Testing recommendation (Validation request 3)

Documented in TASKS.md: the current pyramid (unit + Testcontainers
integration + migration check + security scans) is right-sized — keep it.
The `.http` suite covers manual/exploratory/admin testing. Deliberately not
added now (overhead > value pre-launch): Playwright/Cypress E2E (revisit
with 1–2 smoke journeys after the post-refactor UI stabilizes), k6 load
tests (no real traffic patterns yet), mutation testing, frontend unit tests.
Rule adopted: every manually found bug becomes an integration test.

## 6. TASKS.md (Goal 2) — structure delivered

1. **Phase 1 — Finish locally**: smoke/walkthrough checklists, refactor loop
   (with Claude Opus), seed-address verification (go-live blocker), free
   external accounts (Google OAuth, Resend, domain).
2. **Phase 2 — Deploy-ready before costs**: Azure-for-Students setup, budget
   alerts, hybrid hosting per ADR 0008 (~3–9 EUR/mo table incl. ghcr.io
   instead of ACR), CI/CD staging-auto/prod-manual pipeline with rehearsed
   rollback, minimal analytics ("do people use it": built-in PII-free events
   + App Insights availability ping), **operator runbook/quick-fix guide**
   spec (symptom → 3 commands → fix → verify; AI-executable), legal review
   gate.
3. **Phase 3 — Social validation**: TikTok-first (+IG/YT Shorts) content
   formats, 0-EUR organic plan, pre-committed metric thresholds and the
   deploy trigger (incl. Jan/Sep Austrian gym-signup waves).
4. **Phase 4 — Safe go-live**: T-1 pre-flight, 9-step deployment sequence,
   week-1 daily watch list, launch amplification.
5. **Phase 5 — Monetization**: ads with the GDPR/CMP consequence analysis
   (contextual/direct deals favored over AdSense initially), B2B provision
   models as comparison platforms use them (tracked links, **promo codes as
   the frictionless first offer**, fixed partner listings), pitch metrics
   from existing analytics, contract essentials, neutrality clause
   (paid never influences scores), pilot-studio strategy.

Each phase ends with an explicit exit gate.

## 7. Files changed

| Change | Files |
| --- | --- |
| Design system rewrite | `frontend/app/globals.css` |
| Component redesign | `frontend/components/Scores.tsx` |
| Page restructuring | `app/layout.tsx`, `app/page.tsx`, `app/studios/page.tsx`, `app/studios/[slug]/page.tsx`, `app/rechtliches/[doc]/page.tsx` |
| Fonts (self-hosted) | `frontend/package.json` (+lock): `@fontsource-variable/inter`, `@fontsource-variable/space-grotesk` |
| HTTP client suite | `backend/http/` (9 request files, env files, README) |
| Roadmap | `TASKS.md` (root) |
| Docs | `docs/adr/0010-frontend-design-system.md`, `README.md`, `docs/onboarding.md`, `.gitignore` |

Untouched by design: all backend source, migrations, tests, CI, Bicep,
CONSTRAINTS/AGENTS/Prompt.

## 8. Known limitations / open points

- Integration tests should be re-run once Docker Desktop is up
  (`dotnet test backend/tests/Gym.IntegrationTests`) — expected green, no
  backend changes were made.
- Secondary pages (konto, kontakt, transparenz, case pages) inherit the new
  design system via shared classes; they were visually spot-checked
  (kontakt) but a full click-through per TASKS.md Phase 1.2 is still the
  user's task.
- Staging/production entries in `http-client.env.json` point at the final
  domains; they return errors until Phase 4 deployment.
- Fontsource packages pin variable fonts at build time — no action needed,
  but font updates arrive via `npm update` like any dependency.

## 9. Next three practical steps

1. Run TASKS.md Phase 1.1–1.2 (the `.http` walkthrough + browser feature
   pass with `docker compose up`) to build personal code ownership before
   refactoring.
2. Start Phase 1.5 account creation (Google OAuth client, Resend, domain
   registration) — free, unblocks staging later.
3. Begin the Phase 3 content engine early (build-in-public clips) — audience
   growth has the longest lead time of everything on the roadmap.
