# WhatTheGym — Implementation Result 4

Date: 2026-08-31 (late session) · Executed: full Austrian gym research,
Vienna catalog rebuild, inert per-Bundesland catalogs, SOLID seed refactor
Status: **Complete — 92-studio Vienna seed (81 verified), 122 regional
reference entries, 1,254/1,254 tests green**

## 1. Owner decisions implemented

1. The "~50 studios" cap was removed from CONSTRAINTS.md (owner decision,
   documented in **ADR 0011**). The Vienna seed now contains every studio
   verifiable from official sources.
2. Every other Bundesland got its own catalog class — **pure reference data**:
   compiled, but not seeded, not tested, not wired to anything. Ready to
   connect when expansion happens (will also need domain changes: city/region
   on Gym instead of Vienna-only districts).
3. `Amenities`/`Chains` moved out of `ViennaCatalog` into `SeedVocabulary`
   (SRP: platform-wide vocabulary, not Vienna data); seed records moved to
   `SeedModels.cs` (`SeedGym` for Vienna, `RegionalSeedGym` city-based).

## 2. Research method (WKO-first plan, sitemap-first reality)

Four research agents were launched (Vienna, East, South, West); two stalled
and two crawled — so the research was redone directly: robots.txt → official
chain sitemaps → studio pages, with addresses extracted from JSON-LD or page
text via regex. firmen.wko.at is JS-only and not script-readable; official
chain studio finders are the authoritative sources anyway. All fetched
2026-08-31; drift expected over months/years (dates recorded in file headers).

Sources that worked: fitinn.at (Yoast sitemap + studio pages),
clever-fit.com (studio sitemap + JSON-LD), mrssporty.at (club finder + pages),
johnharris.at (embedded studio JSON), holmesplace.at (JSON-LD),
fitfabrik.at (WP sitemap + pages), johnreed.fitness (club page),
mygym.at (standorte page), crossfitvienna.at.
Not script-readable (entries kept but flagged unverified): mcfit.com,
clubdanube.at, kieser.com, trainingslager.at, happyfit.at, injoy.at,
firmen.wko.at.

## 3. Vienna catalog: 50 → 92 studios

| Chain | Old | New | Finding |
| --- | --- | --- | --- |
| FitInn | 12 | **31** | complete network; 10 of 12 old addresses were wrong |
| clever fit | 5 | **13** | 3 old branches didn't exist |
| Mrs.Sporty | 4 | **16** | 3 old entries not in the official club list |
| John Harris | 4 | **8** | DC Tower is district 22, Hauptbahnhof d10, +4 clubs |
| Fit Fabrik | 5 | **7** | all 5 old addresses were wrong |
| Holmes Place | 3 | **3** | all 3 addresses corrected (Wipplingerstr. 30, Huetteldorfer Str. 130a, Wehlistr. 66) |
| JOHN REED | 0 | **1** | new chain (Wien Oper, Kaerntner Ring 5) |
| McFIT | 5 | 5 | kept, unverified (site blocked) |
| Club Danube | 4 | 4 | kept, unverified |
| Kieser | 3 | 2 | Innere Stadt removed — that address is now JOHN REED |
| MYGYM | 2 | **0** | Salzburg chain, has NO Vienna branches — removed |
| Independents | 3 | 2 | CrossFit Vienna → Rennweg 97 (verified); Doorbreaker dead → removed |

## 4. Regional catalogs (inert, `Seeding/Regional/`)

~122 entries across 8 files + `AustrianRegionalCatalogs` aggregator:
Niederoesterreich 43, Oberoesterreich 22, Steiermark 17, Salzburg 16
(MYGYM home turf), Kaernten 10, Tirol 8, Vorarlberg 4, Burgenland 2.
Entries with confirmed branch but no machine-readable address carry an empty
address string. Known gaps for later: McFIT Graz/Linz/Salzburg/Innsbruck,
Happy Fit (Styria), INJOY, regional independents.

## 5. Test/infra adaptations

- Old seed slugs in integration tests remapped (fitinn-thaliastrasse →
  fitinn-wien-kendlerstrasse etc.); `http-client.env.json` gymSlug updated to
  `fitinn-wien-mariahilfer-strasse`.
- Demo-seed regression test now pages over the larger catalog; one search
  theory term updated ("bruenner" → "gasometer") because the old term only
  existed in a removed entry name.
- `SeedVocabulary` gained the JOHN REED chain; Kieser website updated to
  kieser.com.

## 6. Validation

| Check | Result |
| --- | --- |
| `dotnet build` (warnings as errors) | 0 errors, 0 warnings |
| Domain / Application / Integration tests | **535 / 420 / 299 — all green, 0 skipped** |
| Docs | CONSTRAINTS.md updated (owner-authorized), ADR 0011, seed-data.md rewritten, TASKS.md 1.4 updated |

## 7. Answer: is this useful long-term?

Yes: the regional catalogs turn "expand to Graz" from a research project into
a wiring task (domain city/region support + seed hookup + tests). The data
will drift, but every file carries its research date and source list, and the
verification ledger (seed-data.md) defines the re-check process. Vienna now
also ships with a materially more credible catalog for launch (92 studios,
88 % verified addresses vs. ~4 % before).

## 8. Remaining follow-ups

1. Manually verify the 11 flagged Vienna entries (McFIT/Club Danube/Kieser/
   Trainingslager) in a browser before staging (TASKS 1.4).
2. Optionally enrich regional catalogs with the not-yet-covered chains when
   expansion planning starts.
3. Two research agents (south/west) may still deliver late results — treat
   as cross-check input only; the shipped data comes from direct fetches.
