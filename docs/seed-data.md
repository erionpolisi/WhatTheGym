# Seed data

## Vienna gym catalogue

`Gym.Infrastructure/Seeding/Data/catalog.json` currently seeds 21 chains,
12 amenities and 108 studios across Vienna's districts (rebuilt 2026-08-31
from official chain sources and supplemented from WKO Firmen A-Z on
2026-10-10 and individual official websites through 2026-10-13; see "Catalog
rebuild" below). It is an embedded resource loaded
through the existing `ViennaCatalog` and `SeedVocabulary` APIs (shared
vocabulary, ADR 0011). Seeding is idempotent (keyed by slug) and deterministic
(fixed timestamps), so repeated startups never duplicate data.

**Provenance:** Entries were verified against machine-readable official
sources or WKO Firmen A-Z (studio pages/sitemaps of fitinn.at,
clever-fit.com, mrssporty.at, johnharris.at, holmesplace.at, fitfabrik.at,
johnreed.fitness, mcfit.com, evofitness.at, golds-gym.at, crossfitvienna.at,
and WKO-listed studio websites).

The 2026-10-10 WKO pass inspected all 324 Vienna results through the site's
stateful "Mehr laden" pagination. Only entries with a WKO-listed website were
considered. Existing addresses, hotels, equipment sellers, non-gym businesses,
inactive/liquidating companies, EMS-only studios, Pilates/therapy offers and
private-session concepts were excluded. The catalogue targets conventional,
customer-facing gyms suitable for a broad audience. Verified independent
studios are represented by one chain entry so their official website remains
available through the existing data model.

Opening hours are intentionally not seeded — they are optional and must come
from official data entered by an admin. No Google Maps integration.

## Catalog verification CLI

Run the interactive catalog tool from `backend/`:

```powershell
dotnet run --project tools\Gym.CatalogCli
```

The CLI reads and atomically updates the embedded source file
`src/Gym.Infrastructure/Seeding/Data/catalog.json`. It lists numbered chains
and their websites, then numbered Vienna locations for the selected chain.
Selecting a location opens a normal Google Maps search URL in the default
browser; the tool does not use the Google Maps API or require credentials.

Website verification is stored as `WebsiteVerifiedOn` on a chain and address
verification as `LocationVerifiedOn` on a Vienna gym. Confirming a verification
uses today's date by default and accepts an optional `YYYY-MM-DD` override.
Changing a website or location address clears the corresponding verification
date. The first version can edit and add chains and locations but deliberately
does not delete them. These JSON fields prepare later database persistence;
the CLI itself only changes the seed source. In an interactive terminal,
unverified dates and their per-chain location counts are red. Dates older than
one calendar month and their counts are orange.

Vienna gym entries may optionally set `TemporarilyClosed` to `true`. Missing
values default to an active gym. The seeder maps this flag to the existing
`GymStatus.TemporarilyClosed`, while the CLI highlights it in orange and allows
the status to be toggled without deleting the location.

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
| Mrs.Sporty | 4 (3 not in club list) | 13 | active clubs from mrssporty.at; three closed Vienna club pages removed on 2026-10-13 |
| John Harris | 4 (2 wrong) | 8 | embedded studio JSON on johnharris.at |
| Fit Fabrik | 5 (all wrong) | 7 | fitfabrik.at studio pages |
| Holmes Place | 3 (all 3 addresses wrong) | 3 | JSON-LD on club pages (Wipplingerstr. 30 / Huetteldorfer Str. 130a / Wehlistr. 66) |
| JOHN REED | 0 | 1 | new chain; Wien Oper, Kaerntner Ring 5 |
| McFIT | 5 | 11 | replaced from the official McFIT studio API on 2026-10-12 |
| Club Danube | 4 | 4 | retained in 2026-08, then removed after the abandoned domain was repurposed for casino SEO content |
| Kieser Training | 3 | 0 | removed after Vienna disappeared from the official Kieser studio locator |
| MYGYM | 2 | 0 | chain has NO Vienna branches (Salzburg-based) - removed |
| EVO Fitness | 0 | 9 | active Vienna clubs from the official API; two opening-soon clubs excluded |
| Gold's Gym | 0 | 1 | Wien Columbusplatz from the official club page |
| GET UP Fitness | 0 | 1 | Trabrennstrasse 4 from the official website |
| MH76 Training | 0 | 0 | chain removed; official imprint identifies it as a trainer-room rental platform |
| Independents | 3 | 2 | CrossFit Vienna moved to Rennweg 97 (verified); Doorbreaker removed (domain dead); Trainingslager was retained then and removed during the 2026-10-10 cleanup |
| **Total** | **50** | **92** | |

## Regional catalogs (inert reference data, ADR 0011)

`Gym.Infrastructure/Seeding/Data/catalog.json` holds one catalog per remaining
Bundesland (~122 entries researched 2026-08-31 from the same official chain
sources; entries with an empty address have a confirmed branch without a
machine-readable street address). They are embedded but referenced by nothing:
not seeded, not tested, not exposed. Counts: Niederoesterreich 43,
Oberoesterreich 22, Steiermark 17, Salzburg 16, Kaernten 10, Tirol 8,
Vorarlberg 4, Burgenland 2. Chains not yet covered there (McFIT Graz/Linz,
Happy Fit, INJOY, Kieser, regional independents) can be appended when
expansion becomes real.
