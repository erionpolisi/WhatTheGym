# WhatTheGym — Implementation Result 3

Date: 2026-08-31 · Executed: full step-by-step review, research-backed
architecture assessment, TASKS.md Phase 1 completion, go-public compliance
research, and a ~1,000-test TDD edge-case campaign
Status: **Complete — 1,254 tests green, 7 real defects found and fixed**

## 1. Step 1 — The end product (review yardstick)

The public end product is *the trustworthy, beautiful answer to "welches Gym
in Wien?"*: a sub-second dark UI with huge, honest scores and a visible score
basis; verified, never-anonymous reviews; membership pain (Vertrag, Abrechnung,
Kuendigung) as first-class data no competitor shows; a bulletproof
report→case→decision→appeal legal flow operable by one person via Swagger/
`.http`; ~0–9 EUR/month infrastructure; PII-free analytics; SEO-compounding
district/chain pages; a runbook that lets one person diagnose anything in
under 10 minutes; later, labeled neutral partner revenue that never touches
ranking. Quality bar: every constraint-relevant behavior is guarded by a test
that fails if the rule regresses. The whole session was measured against this.

## 2. Step 2 — Architecture review (research-backed, change only if necessary)

Method: full codebase mapping (explore agent, 45 findings), independent
reading of the decision-critical files (ScoreCalculator, Review entity,
Result pattern, Program.cs auth/rate limiting, SessionService), primary-source
legal research, and 1,159 new tests as empirical verification.

**Verdict: the architecture is sound — no structural change is necessary.**
Monolith + Clean Architecture, CQRS-light, Result pattern, PostgreSQL FTS,
BFF cookie auth, materialized summaries, outbox mail, soft-delete + retention
sweeper: all confirmed appropriate for a solo-operated, cost-capped MVP and
consistent with CONSTRAINTS.md. The score aggregation implements the 50/50 /
`scoreBasis` / never-zero rules exactly (now proven by a 114-case matrix).

Necessary, surgical changes made (all verified by tests):

| # | Fix | Why necessary |
| --- | --- | --- |
| 1 | Demo seeder drew case number `WTG-2026-000001` without consuming `legal_case_seq` → first real report on a dev DB failed with a 500 unique-index violation | Found live in the Phase-1 walkthrough; TDD: red regression test (`DemoSeedRegressionTests`), then fix |
| 2 | Session cookie `SecurePolicy` and refresh cookie `Secure` flag depended on the inbound scheme → behind a TLS-terminating proxy without forwarded headers, auth cookies could be sent non-Secure | CONSTRAINTS mandates Secure cookies; now `Always` outside Development (ADR 0003 amendment) |
| 3 | Fast-track hide could move a soft-deleted review to `UnderReview`, so a later release would resurrect author-deleted content | Domain invariant hole (defense-in-depth); guard added in `Review.PlaceUnderLegalReview` |
| 4 | Account deletion skipped soft-delete for reviews under legal hold → content stayed PUBLIC against the author's GDPR deletion request | Holds pause retention *purging*, never public visibility (ADR 0007 intent); fixed in `DeleteMyAccountCommandHandler` |
| 5 | `ModeratorRemoveReviewCommand` carried the actor role but never checked it | The command's contract implies handler-level enforcement; Forbidden guard added |
| 6 | Gym create/update validators accepted `null` postal code (`Matches` ignores null) → DB-level 500 instead of German 400 | `NotEmpty()` added |
| 7 | 429 responses had no `Retry-After` header | RFC-conform client guidance; one-line `OnRejected` |

Explicitly reviewed and **kept** (documented as design, with tests that pin
the behavior): email *format* validation lives in API validators, not entity
factories (layering); XSS defense is output encoding (JSON API + React
escaping), not server-side markup stripping; API `[Authorize]` policies are
the authorization boundary (handler guards only where the command carries the
role); `SameAsRequest` cookies in Development for plain-HTTP Docker. Optional
non-necessary refactors (slug-helper dedup, enum-parse helper, honeypot-hit
logging, outbox batching) are listed in TASKS.md 1.3 — deliberately NOT done
(KISS, "change only if necessary").

## 3. Step 3 + Phase 1 — Go-public research and TASKS.md completion

### Phase 1 status (automatable parts executed)

- Stack, migrations, seed: green. 45-check API walkthrough executed against
  the live stack (found defect #1). Frontend build green.
- **Seed data (1.4)**: automated verification of all 50 studios against
  official sources — result: **2 verified, 27 need correction (every FitInn,
  Fit Fabrik and clever fit entry!), 20 unverifiable, 1 likely closed
  (Doorbreaker)**. Full worksheet appended to `docs/seed-data.md`; TASKS 1.4
  updated. This is the largest remaining go-live blocker and needs manual
  work-through.
- 1.5 (Google OAuth client, Resend, domain) remains user-only.

### Compliance research (primary sources; folded into TASKS.md §2.7)

Key findings for Austria/EU 2025/2026:

- **DSA applies despite micro size** for Arts. 11/12 (contact points), 14
  (terms content), 16 (notice-and-action — already built), 17 (statement of
  reasons — already built); transparency reports etc. are exempt (Art. 19).
- **EU ODR platform discontinued 20 July 2025** — never link it.
- **EAA/BaFG: exempt** (microenterprise, Art. 4(5)); WCAG stays voluntary.
- **No cookie banner needed** (TKG §165(3)) for session cookies + the
  cookie-less PII-free analytics — re-check when ads arrive.
- **Highest product-level risk: the "Verifiziert ueber Google" badge** may be
  misleading under UWG/Omnibus (implies verified gym usage). Mitigation
  shipped now: badge tooltip + disclosure sentence on review lists
  ("Ein tatsaechlicher Besuch des Studios wird nicht ueberprueft"); relabel
  decision flagged [LAWYER] in TASKS.md (would touch CONSTRAINTS wording).
- RoPA required (processing is "not occasional"); Impressum needs ECG §5 +
  MedienG §25 combined; Gewerbe/SVS/Werbeabgabe trigger only at monetization;
  trademark search checklist added.

## 4. The test campaign (TDD, edge-case deep dives)

Three sequential test-engineer agents (new files only, no production edits,
bug reports instead of silent fixes), plus central triage/fixes by hand:

| Suite | Before | After | New cases | Focus |
| --- | --- | --- | --- | --- |
| `Gym.Domain.Tests` | 40 | **535** | 495 | 114-case scoring matrix (bases, 50/50 exactness, AwayFromZero midpoints, large inputs, never-zero), 225 review invariants (11 categories × bounds, sanitizer interaction, all deletion origins, every legal transition), legal-case state machine incl. appeal deadline boundaries, slugs/umlauts, catalog guards, token rotation entities, Result primitives |
| `Gym.Application.Tests` | 22 | **420** | 398 | boundary matrix for EVERY validator (null/empty/min−1/min/max/max+1, enums, emails, districts 0/1/23/24, paging), handler behavior over fakes (error codes, slug collisions, summary recompute, mail enqueueing, German messages), full legal command flow with token-hash assertions |
| `Gym.IntegrationTests` | 33+1 | **299** | 265 | 23-district filter matrix, sort/pagination/term/typo/umlaut search, auth contract (rotation, reuse→family revocation, role×endpoint 401/403 matrix), review lifecycle vs summaries, fast-track hide/release over HTTP, honeypots end-to-end, ETag 304, analytics allowlist, legal documents, append-only DB trigger (UPDATE on LegalCaseEvent throws), dedicated low-limit factory proving 429 + Retry-After |
| **Total** | 95 | **1,254** | **1,159** | — |

TDD discipline: the walkthrough 500 became a red regression test before the
fix; agent bug reports were triaged into 7 real fixes (each now un-skipped and
green) and 5 documented design decisions (tests renamed to pin the intended
behavior instead of being deleted). Zero skipped tests remain.

## 5. Validation results (all executed locally, 2026-08-31)

| Check | Result |
| ----- | ------ |
| `dotnet build WhatTheGym.sln` (warnings as errors) | 0 errors, 0 warnings |
| `Gym.Domain.Tests` | **535/535 passed** |
| `Gym.Application.Tests` | **420/420 passed** |
| `Gym.IntegrationTests` (Testcontainers, real HTTP) | **299/299 passed** (~15 s) |
| Frontend `npm run build` | ✓ 13/13 pages |
| `docker compose up --build` (fixed image) | healthy |
| Live smoke: report on demo-seeded DB | **201, `WTG-2026-000002`** (was 500) |
| API walkthrough (45 checks) | 44/45 pass (1 script artifact) |

## 6. Files changed

| Area | Files |
| --- | --- |
| Production fixes | `DatabaseSeeder.cs`, `Program.cs` (cookies, Retry-After), `SessionService.cs`, `Reviews.cs`, `UserHandlers.cs`, `ReviewQueries.cs`, `GymCommands.cs` |
| Frontend compliance | `app/studios/[slug]/page.tsx`, `components/UserForms.tsx` (badge tooltip + disclosure) |
| New tests | 8 domain files, 4 application files, 8 integration files (+`DemoSeedRegressionTests` factory) |
| Docs | `TASKS.md` (Phase 1 checkoffs, §2.7 compliance, test table), `docs/testing.md` (new), `docs/seed-data.md` (verification worksheet), `docs/adr/0003` (amendment), `README.md` |

## 7. Open points (explicitly not silently changed)

1. **Seed addresses**: work through the docs/seed-data.md worksheet before
   staging (go-live blocker, TASKS 1.4).
2. **Badge relabeling** ("Google-Konto verifiziert"): [LAWYER] decision —
   would change CONSTRAINTS wording, so only the disclosure was shipped.
3. TASKS 1.5 accounts (Google OAuth, Resend, domain) are user-only actions.
4. Optional refactors listed in TASKS 1.3 (dedup helpers, honeypot logging,
   outbox batching) — valuable but not necessary; do them during your
   personal read-through.

## 8. Next three practical steps

1. Work the seed-data verification worksheet (fix the 27 corrections, decide
   the 21 unverifiable, drop Doorbreaker) — the last data blocker.
2. Do your personal Phase-1.3 read-through with the new test suites as a
   safety net (any refactor that stays green is safe).
3. Start TASKS Phase 2.1/2.7: Azure student account + the four urgent legal
   items (Impressum draft, badge decision, T&C DSA content, RoPA review) so
   the lawyer round covers everything at once.
