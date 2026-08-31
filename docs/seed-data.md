# Seed data

## Vienna gym catalogue

`Gym.Infrastructure.Seeding.ViennaCatalog` seeds 11 chains, 12 amenities and
92 studios across Vienna's districts (rebuilt 2026-08-31 from official chain
sources; see "Catalog rebuild" below). Chains and amenities live in
`SeedVocabulary` (shared vocabulary, ADR 0011). Seeding is idempotent (keyed
by slug) and deterministic (fixed timestamps), so repeated startups never
duplicate data.

**Provenance:** 81 of 92 entries were verified against machine-readable
official sources on 2026-08-31 (studio pages/sitemaps of fitinn.at,
clever-fit.com, mrssporty.at, johnharris.at, holmesplace.at, fitfabrik.at,
johnreed.fitness, crossfitvienna.at). 11 entries (McFIT ×5, Club Danube ×4,
Kieser ×2, Trainingslager) are marked unverified in the catalog comments —
their chain sites were not script-readable; re-verify before production
import (ADR 0009).

Opening hours are intentionally not seeded — they are optional and must come
from official data entered by an admin. No Google Maps integration.

## Legal documents

Version 1 of Impressum, Datenschutzerklaerung and Nutzungsbedingungen is
seeded and published in every environment so the public legal endpoints work.
All texts are drafts: `ENTWURF - anwaltlich pruefen lassen`.

## Demo data (local/Development only)

With `Seed:SeedDemoData=true` **and** the Development environment, the seeder
adds three demo users, a deterministic pattern of reviews over the first six
studios (covering `both`, `membershipOnly` and `studioOnly` score bases), the
corresponding materialized summaries, and one demo legal case
(status `Received`; its number is drawn from the shared `legal_case_seq`
sequence so real reports never collide with it). Demo data never reaches
staging or production; the flag is ignored outside Development.


## Catalog rebuild (2026-08-31)

Full re-research replacing the original ~50-entry best-effort list:

| Chain | Old | New | Notes |
| --- | --- | --- | --- |
| FitInn | 12 (10 wrong addresses) | 31 | complete Vienna network from fitinn.at studio pages |
| clever fit | 5 (3 nonexistent branches) | 13 | from clever-fit.com de-AT sitemap + JSON-LD addresses |
| Mrs.Sporty | 4 (3 not in club list) | 16 | complete list from mrssporty.at club finder |
| John Harris | 4 (2 wrong) | 8 | embedded studio JSON on johnharris.at |
| Fit Fabrik | 5 (all wrong) | 7 | fitfabrik.at studio pages |
| Holmes Place | 3 (all 3 addresses wrong) | 3 | JSON-LD on club pages (Wipplingerstr. 30 / Huetteldorfer Str. 130a / Wehlistr. 66) |
| JOHN REED | 0 | 1 | new chain; Wien Oper, Kaerntner Ring 5 |
| McFIT | 5 | 5 | kept unverified (site not script-readable) |
| Club Danube | 4 | 4 | kept unverified (site JS-only) |
| Kieser Training | 3 | 2 | Innere Stadt removed (address now verified as JOHN REED) |
| MYGYM | 2 | 0 | chain has NO Vienna branches (Salzburg-based) - removed |
| Independents | 3 | 2 | CrossFit Vienna moved to Rennweg 97 (verified); Doorbreaker removed (domain dead); Trainingslager kept unverified |
| **Total** | **50** | **92** | |

## Regional catalogs (inert reference data, ADR 0011)

`Gym.Infrastructure.Seeding.Regional.*` holds one catalog per remaining
Bundesland (~122 entries researched 2026-08-31 from the same official chain
sources; entries with an empty address have a confirmed branch without a
machine-readable street address). They are compiled but referenced by
nothing: not seeded, not tested, not exposed. Counts: Niederoesterreich 43,
Oberoesterreich 22, Steiermark 17, Salzburg 16, Kaernten 10, Tirol 8,
Vorarlberg 4, Burgenland 2. Chains not yet covered there (McFIT Graz/Linz,
Happy Fit, INJOY, Kieser, regional independents) can be appended when
expansion becomes real.
