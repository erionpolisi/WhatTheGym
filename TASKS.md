# TASKS.md — Road to Launch (and beyond)

The single checklist of everything between "works on my machine" and a
publicly running, cheap, observable, monetizable `whatthegym.at`.
Ordered by phase; each phase has an explicit **exit gate** — do not start the
next phase before the gate is met. Solo-operator friendly: everything assumes
one person with limited time and a ≤ 10 EUR/month budget (ADR 0008).

Status legend: `[ ]` open · `[x]` done · `[~]` partially done / prepared.

---

## Phase 1 — Finish and own the code locally

Goal: you can run, test, explain, and confidently change every feature.
Tooling: Rider + `backend/http/` request suite (manual testing) + the
automated test pyramid (see "Testing strategy" at the bottom).

### 1.1 Environment & smoke

- [x] `docker compose up --build` starts db + API, migrations apply, seed runs
- [x] Unit tests green (`Gym.Domain.Tests`, `Gym.Application.Tests`)
- [x] Integration tests green (`Gym.IntegrationTests`, needs Docker)
- [x] Frontend `npm run build` green, pages render against the live API
- [x] Walk every `backend/http/*.http` file top-to-bottom once — executed as
      a scripted 45-request API walkthrough (2026-08-31): found and fixed the
      demo-seed case-number collision (500 on first real report). Re-walk
      manually in Rider whenever you want to learn the surface hands-on.

### 1.2 Feature walkthrough (manual, once, in the browser)

All flows below were verified end-to-end against the live local stack on
2026-08-31 (scripted, 45 checks: 44 pass after the seed fix; the one script
artifact was a false negative). Re-do in the browser for UX feel:

- [x] Search/filter studios (term, Bezirk, Kette, Mindestscore, Sortierung)
- [ ] Gym detail in browser: score hero, category bars, amenities, hours (API verified; visual pass pending)
- [x] Dev-login → write review → score updates → edit review → delete review
- [x] Report a review → case status link → admin: classify/decide/close
      → transparency report reflects it
- [x] Contact form (all 3 types) + honeypot behavior
- [x] Konto: export JSON, account deletion (integration-tested; browser pass optional)
- [x] Legal pages render from backend (Impressum/Datenschutz/Nutzungsbedingungen)

### 1.3 Refactor & personal code review

- [~] Read through each project in dependency order — an assisted full-code
      review (2026-08-31, Results/Result3.md) found the architecture sound;
      fixed: seed case-number collision, cookie Secure policy outside
      Development. Remaining candidates are listed in Result3 §review as
      OPTIONAL (slug-helper dedup, enum-parse helper, honeypot logging).
      Your personal read-through for ownership is still worth doing.
- [ ] Refactor with Claude Opus feature-by-feature; after EVERY refactor run:
      `dotnet build` + unit tests + affected integration tests (keep green)
- [ ] Delete/park anything you do not understand — you are the only operator;
      unknown code is operational risk

### 1.4 Data correctness (go-live blocker, ADR 0009)

- [x] Catalog rebuilt 2026-08-31 (ADR 0011): 92 Vienna studios, 81 verified
      against official chain sources (fitinn.at, clever-fit.com, mrssporty.at,
      johnharris.at, holmesplace.at, fitfabrik.at, johnreed.fitness,
      crossfitvienna.at). Details: docs/seed-data.md "Catalog rebuild".
- [ ] Re-verify the 11 remaining unverified entries manually in a browser
      (McFIT ×5, Club Danube ×4, Kieser ×2 — sites are not script-readable)
      and spot-check opening data before staging
- [x] Doorbreaker removed (domain dead), MYGYM Vienna entries removed (chain
      has no Vienna branches), CrossFit Vienna corrected to Rennweg 97
- [x] Confirm demo reviews/cases are Development-only (re-verified; demo case
      number drawn from the shared sequence — collision bug fixed)

### 1.5 External services (create now, still free)

- [ ] Google Cloud project + OAuth consent screen (external) + OAuth client;
      redirect URIs for staging AND production; test the REAL Google login
      locally via staging config as soon as DNS exists
- [ ] Resend account + verify sending domain (`whatthegym.at`) — free tier
      (100 mails/day) is plenty; test one mail through the outbox
- [ ] Register `whatthegym.at` (~15–30 EUR/year — the one unavoidable cost;
      e.g. easyname/World4You/INWX, or Cloudflare Registrar after transfer)

**Exit gate 1:** all boxes above checked; you can demo every feature without
looking anything up; addresses verified; tests green.

---

## Phase 2 — Deploy-ready on Azure (before any costs start)

Goal: everything is prepared, calculated, and rehearsed so that "go live" is
one deliberate action, not a research project. Use the **Azure for Students**
subscription (100 USD credit / 12 months, no credit card) — effectively a
free staging year; re-evaluate cost before credit expiry.

### 2.1 Accounts & foundation (free)

- [ ] Activate Azure for Students; note credit expiry date in your calendar
- [ ] Create resource groups `wtg-staging` and `wtg-prod`
- [ ] Set a **budget alert** (Cost Management): alert at 1 EUR, 5 EUR, 10 EUR
      forecast — this is your seatbelt as a solo dev
- [ ] Decide hosting per ADR 0008 (recommended hybrid, ~0–7 EUR/mo):
      - API: Container Apps, scale-to-zero, 0.25 vCPU/0.5 GiB
      - Frontend: Static Web Apps Free tier
      - DB: external free/cheap managed PostgreSQL (e.g. Neon/Supabase free
        tier) OR Azure Flexible Server B1ms (~13 EUR — over cap, documented)
      - Secrets: Key Vault (pennies), Logs: Log Analytics with 1 GB/day cap
- [ ] Container registry: GitHub Container Registry (ghcr.io, free) instead
      of ACR — saves ~5 EUR/mo; document in ADR 0008 addendum

### 2.2 Cost calculation (write the numbers down BEFORE deploying)

- [ ] Fill this table with current prices, commit it to ADR 0008:

  | Item | SKU | Est. EUR/month |
  | --- | --- | --- |
  | Container Apps (scale-to-zero, low traffic) | Consumption | 0–3 |
  | Static Web Apps | Free | 0 |
  | PostgreSQL (external free tier) | Free | 0 |
  | Key Vault | Standard, few ops | <1 |
  | Log Analytics (capped 1 GB/day, 30d retention) | PerGB | 0–2 |
  | App Insights (sampled) | — | 0–1 |
  | DNS zone | — | ~0.4 |
  | Domain (yearly /12) | — | ~2 |
  | **Total** | | **~3–9** |

- [ ] Define the "abort criteria": at which monthly cost do you scale down /
      switch DB / shut off staging? Write it into the runbook.

### 2.3 CI/CD pipeline (dev → staging → prod swap)

- [ ] Extend GitHub Actions: on `main` push → build/test (exists) → build
      image → push to ghcr.io → deploy **staging** (az containerapp update)
- [ ] Production deploys ONLY via manually triggered workflow
      (`workflow_dispatch`) or git tag `v*` — never automatic
- [ ] Store Azure credentials as GitHub OIDC federated identity (no static
      secrets), Resend/Google secrets in Key Vault
- [ ] Rollback rehearsed: redeploy previous image tag == the rollback story;
      document the exact command in the runbook
- [ ] Bicep applied once to staging (`az deployment group create` with
      `parameters.staging.json`) — treat first run as validation of the
      prepared-but-never-executed templates

### 2.4 Analytics & monitoring ("do people use it?" — minimal)

- [ ] Built-in PII-free `AnalyticsEvent` is the primary usage signal
      (page_view etc.). Add one tiny read path for yourself: a SQL query
      collection (`docs/runbook.md`) or a single admin endpoint —
      page views/day, reviews/day, top gyms. KISS: SQL queries are enough.
- [ ] App Insights: availability ping on `/health/ready` (free) + failure
      alert to your email — that is 90 % of solo monitoring
- [ ] Serilog → console → Log Analytics (capped); log retention 30 days
- [ ] Optional later: swap/add Plausible/Umami if you want dashboards; NOT
      required for launch

### 2.5 Operator runbook (`docs/runbook.md`) — write it, you will need it

- [ ] "Site is down" checklist: health endpoints → Container App logs
      (`az containerapp logs show`) → DB provider status page → restart
      command → rollback command
- [ ] "Google login broken": check OAuth consent status, redirect URIs,
      secret expiry
- [ ] "Mails not arriving": outbox table query, Resend dashboard, retry-count
      query — with copy-paste SQL
- [ ] "Database migration failed": how to check applied migrations, how to
      restore the pre-deploy backup/snapshot
- [ ] "Legal report arrived": link to the 80-admin-legal.http walkthrough and
      the legal deadlines (react, document decision)
- [ ] Backup/restore: enable + TEST a restore once (external PG: provider
      snapshot; document steps). An untested backup does not exist.
- [ ] Every entry: symptom → 3 diagnostic commands → fix → verification.
      Written so an AI assistant (or you at 2 a.m.) can execute it literally.

### 2.6 Security & legal before exposure

- [ ] Lawyer review of Impressum, Datenschutzerklaerung, Nutzungsbedingungen,
      mail texts, processing-activities record (all currently ENTWURF) —
      **legally required in AT before public operation**, budget a fixed-fee
      review
- [ ] Publish reviewed versions via `80-admin-legal.http`
- [ ] Verify CORS allowlists, cookie flags, rate limits with staging URLs
- [ ] Dependabot/CodeQL/Trivy findings triaged to zero high/critical

### 2.7 Go-public compliance checklist (researched 2026-08-31, primary sources)

Verified against ECG, MedienG, DSA (Reg. 2022/2065), EAA/BaFG, TKG 2021,
UWG/Omnibus, GDPR. Items marked [LAWYER] need professional confirmation.

**Urgent (before any public traffic):**

- [ ] [LAWYER] **Badge wording "Verifiziert ueber Google"**: under the
      Omnibus Directive/UWG it is misleading to imply verified *gym usage*
      when only the Google account is verified. Mitigation shipped: tooltip +
      disclosure text ("Google-Konto bestaetigt — kein Nachweis eines
      Studiobesuchs"). Decide with lawyer whether to relabel to
      "Google-Konto verifiziert" (CONSTRAINTS change → documented decision).
- [ ] Add the UCPD Art. 7 review-authenticity disclosure to Nutzungsbedingungen
      and near review lists: "Wir pruefen nicht, ob Bewertende das Studio
      tatsaechlich besucht haben."
- [ ] Impressum per ECG §5 **plus** MedienG §25 Offenlegung (kleines Medium:
      name + address of Medieninhaber) on one `/impressum` page. A serviceable
      c/o or business address is sufficient — a home address is NOT mandatory,
      but a pure Postfach is not enough. [LAWYER]
- [ ] Do NOT link the EU ODR platform — discontinued 20 July 2025
      (Reg. 2024/3228); remove from any template text.

**DSA duties that apply DESPITE micro exemption (hosting/platform):**

- [ ] Art. 11+12: designate + publish one electronic contact point for
      authorities (RTR) and users (e.g. kontakt@ in Impressum/legal section)
- [ ] Art. 14: Nutzungsbedingungen must state content rules, removal grounds,
      moderation procedure, redress options (feed into lawyer draft)
- [ ] Art. 16: notice-and-action — ALREADY BUILT (report flow); keep the log
      of notices + actions (LegalCase audit trail covers this)
- [ ] Art. 17: statement of reasons on removal/restriction — ALREADY BUILT
      (decision mails with rationale); verify template wording with lawyer
- [ ] Notify RTR-GmbH (Austrian DSC) of contact point (no fee) [LAWYER]
- [x] Exempt as micro: transparency reports, internal complaint system,
      out-of-court bodies, trusted flaggers (DSA Art. 19(1))

**Confirmed NOT required (documented, stop worrying):**

- [x] Cookie banner — session cookies are strictly necessary (TKG §165(3));
      analytics is cookie-less/PII-free → no consent surface. Re-check the
      moment ads or any client-side identifier arrives.
- [x] EAA/Barrierefreiheitsgesetz — microenterprise service exemption
      (EAA Art. 4(5)); keep WCAG AA as voluntary quality bar. Re-audit at
      10 employees / 2M EUR turnover.
- [x] DPO (GDPR Art. 37) and ODR link.

**Before launch (cheap, do once):**

- [ ] RoPA/Verarbeitungsverzeichnis: Art. 30(5) exemption does NOT apply
      (processing is not occasional) — the generated processing-activities
      record exists; review it against an Art. 30 template. [LAWYER]
- [ ] Datenschutzerklaerung: include Google OAuth data-flow disclosure +
      DSB complaint right + retention periods (feed lawyer draft)
- [ ] 72h breach-notification one-pager (who, what, DSB portal dsb.gv.at)
      → runbook entry
- [ ] Trademark search "WhatTheGym": tmview (patentamt.at) + EUIPO eSearch,
      classes 35/41/42 (~20 min; [LAWYER] only on conflict)
- [ ] T&C content rules: forbid fake reviews, owner self-reviews, third-party
      PII in texts; document the 48h response process for injunction letters
      (einstweilige Verfuegung) → runbook entry

**When monetizing (Phase 5 triggers):**

- [ ] Gewerbe registration (freies Gewerbe, GISA) + SVS check at first ad
      revenue; income tax registration [LAWYER]
- [ ] Werbeabgabe 5% on online ad revenue from the first Euro
      (WerbeabgabeG 2000) — register with Finanzamt

**Exit gate 2:** staging runs on Azure at ~0 EUR (credits), one full
staging deploy + rollback executed, budget alerts armed, runbook exists and
was rehearsed once, legal texts at least submitted for review.

---

## Phase 3 — Social-media validation (before public launch)

Goal: know whether anyone wants this BEFORE paying for production, and have
an audience on day 1. Budget: 0 EUR (organic only).

### 3.1 Setup (one weekend)

- [ ] Handles: `whatthegym.at` name consistency — TikTok, Instagram, YouTube
      (Shorts), reserve the name even where you won't post
- [ ] Landing teaser: staging or a one-page "coming soon" with a
      Formspree/contact email signup — measurable interest, zero cost
- [ ] Brand kit: the new dark/orange UI IS the brand — export logo/OG images
      from it; consistent visual identity across all clips

### 3.2 Content engine (TikTok-first, IG Reels = same clips, YT Shorts = same clips)

- [ ] Format A: "Wiener Gym-Preisfallen" — contract/cancellation horror
      stories (anonymized, generic — legally safe wording, no named gym
      accusations without evidence)
- [ ] Format B: "Gym-Check Wien" — 30-second neutral studio walkthroughs/facts
      per district (public info only)
- [ ] Format C: behind-the-scenes "Ich baue eine Gym-Bewertungsplattform fuer
      Wien" build-in-public series — cheap, authentic, algorithm-friendly
- [ ] Cadence: 3–5 clips/week for 6–8 weeks; batch-produce on weekends
- [ ] Every clip: same CTA ("Link in Bio — bewerte dein Gym") once live;
      before launch: "Follow fuer den Launch"

### 3.3 Validation metrics & the launch decision

- [ ] Define thresholds BEFORE posting (adjust to taste):
      - ≥ 500 followers combined OR ≥ 3 clips > 10k views → strong signal
      - ≥ 50 email signups / "wann kommt das?" comments → strong signal
      - < 100 followers and no engagement after 8 weeks → iterate the angle
        (content, not code) before spending anything on production
- [ ] Track weekly in a simple spreadsheet: views, followers, saves, comments
      asking for the product
- [ ] **Deploy trigger**: any strong signal above, OR a planned date you
      commit to (e.g. semester start / New Year fitness wave — the natural
      gym-signup peaks in Austria: January and September)

**Exit gate 3:** thresholds written down and one of the deploy triggers met.

---

## Phase 4 — Production go-live (the safe, boring checklist)

Goal: deploy deliberately, verify everything, be able to roll back in
minutes. Do it on a weekday morning, not Friday 18:00.

### 4.1 Pre-flight (T-1 day)

- [ ] `main` green: build, unit, integration, migration check, scans
- [ ] Staging == the exact image/tag you will promote
- [ ] Fresh DB backup/snapshot taken and restore-tested this week
- [ ] Production secrets in Key Vault: Google OAuth (prod redirect URI!),
      Resend key, connection string, `Auth:BootstrapAdminEmail` = your Gmail
- [ ] Legal documents: lawyer-approved versions published; ENTWURF marker
      removed via the admin API
- [ ] Seed check: production seeds studios/chains/amenities/legal docs ONLY —
      no demo reviews/cases (Development-gated; verify config is Production)

### 4.2 Go-live sequence

1. [ ] Create prod resources via Bicep (`parameters.production.json`)
2. [ ] DNS: `whatthegym.at` → Static Web Apps, `api.whatthegym.at` →
       Container Apps; TLS certs issued (both managed/free)
3. [ ] Deploy API image (the staging-verified tag), run migrations, seed
4. [ ] Deploy frontend with production env (`NEXT_PUBLIC_API_BASE_URL=https://api.whatthegym.at`)
5. [ ] Smoke: `/health/live`, `/health/ready`, homepage, gym detail, sitemap,
       robots (use `00-health.http` + browser)
6. [ ] Real Google login with YOUR account → bootstrap admin role confirmed
       via `/api/v1/me`
7. [ ] One real end-to-end: write review → report it → decide → delete review
8. [ ] Mail proof: contact request → Resend delivers to your inbox
9. [ ] Availability alert on prod `/health/ready` armed

### 4.3 What to watch in week 1 (daily, 10 minutes)

- [ ] Cost Management daily spend (should be cents)
- [ ] App Insights: failures, response times, availability
- [ ] AnalyticsEvent counts: page views, review creations
- [ ] Outbox table: no stuck `Pending`/`Failed` mails
- [ ] Legal reports: any `LegalCase` created → react per runbook (deadlines!)
- [ ] Search Console: submit sitemap, verify indexing starts

### 4.4 Launch amplification

- [ ] Flip social CTA to the live URL, pin a launch clip
- [ ] Post in Vienna contexts (r/wien, local fitness groups) — transparently
      as the builder, not as spam

**Exit gate 4:** 7 quiet days: no unhandled errors, cost on forecast,
backups verified, at least a handful of organic reviews.

---

## Phase 5 — Monetization (post-launch, changes deploy via the pipeline)

Goal: recover costs first, then build the B2B provision business. Sequence
matters: traffic → ads (cost recovery) → partnerships (real money).

### 5.1 Ads (first money, low effort) — target: cover the ~10 EUR/mo

- [ ] Prerequisite: measurable traffic (e.g. > 5–10k page views/month —
      below that, ads earn cents and only hurt the product)
- [ ] **GDPR/TCF consequence (plan BEFORE integrating):** AdSense & co.
      require a TCF 2.2 consent management platform (CMP). Today the site is
      deliberately cookie-consent-free (no banner needed). Adding personalized
      ads = adding a CMP banner + Datenschutz update (lawyer round 2).
      Alternatives to evaluate first:
      - Non-personalized/contextual ads (still needs CMP for AdSense, but
        e.g. EthicalAds/Carbon-style contextual networks do not)
      - Direct fixed-price banner deals with local fitness brands (no CMP,
        no tracking, often better eCPM for niche traffic) ← **KISS favorite**
- [ ] Implementation when chosen: ad slots as opt-in components on list +
      detail pages only; never inside the score hero (trust!); deploy via
      staging → prod pipeline like any feature
- [ ] Document the decision as an ADR (revenue vs. privacy posture)

### 5.2 B2B partnerships & provision (the real goal)

How comparison platforms solve "did this signup come from me?" — standard
models, pick per partner:

- [ ] **Tracked referral links** (industry standard): outbound
      "Zum Angebot" button per gym → partner URL with `?ref=whatthegym`/UTM
      or the partner's affiliate program if one exists (e.g. via Awin/
      TradeTracker networks where gym chains list). Partner's system counts
      conversions; you invoice on their numbers.
- [ ] **Voucher/promo codes** (works without ANY tech integration):
      partner issues "WHATTHEGYM" code (ideally + member discount) → every
      redemption is attributable, verifiable by both sides, and gives users
      a reason to click. **Best first offer to a gym: zero effort for them,
      clean attribution, user benefit.**
- [ ] **Fixed monthly listing upgrades** (no attribution problem at all):
      "verified partner" profile, response-to-reviews right, official photos
      — flat fee, no conversion tracking dispute possible. Easiest to sell
      once traffic per gym page is demonstrable.
- [ ] Build the pitch metric first: per-gym monthly profile views + clicks
      from the existing PII-free analytics (add `outbound_click` to the
      allowlist — small, privacy-clean change)
- [ ] Legal hygiene for credibility: paid placements must be labeled
      ("Anzeige"), and **paid partnerships must NEVER influence scores or
      ranking** — write this into Transparenz page + partner contract;
      the platform's value IS its neutrality (HeyDoc/idealo model)
- [ ] Contract essentials (lawyer round 3, template once, reuse): provision
      per redeemed code / per tracked signup, monthly reporting duty of the
      partner, audit right on redemption counts, 3-month notice, no score
      influence clause
- [ ] Start with ONE friendly independent studio as pilot (not a chain HQ);
      chains follow proof, not pitches

### 5.3 Feature backlog that supports revenue (post-launch, each via ADR)

- [ ] `outbound_click` analytics event + per-gym stats query
- [ ] Partner badge/profile fields (admin-managed, Swagger is enough)
- [ ] Public "Top studios per district" SEO pages (free traffic compounding)
- [ ] Only then: consider CMP + ad network if direct deals underperform

---

## Testing strategy (answer to "is more testing worth it?")

Current pyramid — **keep exactly this, it is right-sized** (counts after the
2026-08-31 edge-case campaign, 1,254 tests total):

| Layer | Tool | Covers | Verdict |
| --- | --- | --- | --- |
| Unit (Domain 535 / Application 420) | xUnit | invariants, scoring matrices, validator boundaries | keep, extend per feature |
| Integration (299) | Testcontainers PostgreSQL + real HTTP | API contracts, auth, legal flow, search, regressions | keep — this is the valuable layer |
| Migration check | CI job | schema drift | keep |
| Manual/exploratory | `backend/http/` suite | admin ops, error paths, environments | new — use as the daily driver |
| Security | CodeQL, Trivy, Dependabot | dependencies/images | keep |

Deliberately **not** added now (documented overhead, revisit post-launch):

- **Playwright/Cypress E2E**: high maintenance for a thin SSR frontend whose
  logic lives in tested APIs; add at most 1–2 smoke journeys AFTER the UI
  stabilizes post-refactor phase.
- **k6/JMeter load tests**: pointless before real traffic patterns exist;
  scale-to-zero Container Apps + Postgres handle MVP loads trivially.
- **Mutation testing (Stryker.NET)**: nice, not now.
- **Frontend unit tests**: components are presentational; `next build` +
  TypeScript strict already catch the realistic bug class.

Rule of thumb going forward: every bug found manually gets reproduced as an
integration test before it is fixed.
