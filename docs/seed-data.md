# Seed data

## Vienna gym catalogue

`Gym.Infrastructure.Seeding.ViennaCatalog` seeds 10 chains, 12 amenities and
50 studios across Vienna's districts (FitInn, McFIT, clever fit, John Harris,
Holmes Place, Fit Fabrik, Kieser Training, Mrs.Sporty, Club Danube, MYGYM plus
independents). Seeding is idempotent (keyed by slug) and deterministic (fixed
timestamps), so repeated startups never duplicate data.

**Provenance caveat:** studio names, chains and district placements follow
publicly known locations; street addresses are best-effort approximations.
Before any staging/production import, verify every entry against the official
studio websites and correct addresses/opening hours. Track corrections through
the normal `ContactRequest`/admin flow. (See ADR 0009.)

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

## Verification status (2026-08-31, automated web check)

Sources: fitinn.at, johnharris.at, holmesplace.at, clever-fit.com sitemap,
fitfabrik.at sitemap, mcfit.com/at, mrssporty.at, clubdanube.at (timeout),
mygym.at. Automated fetch — JS-rendered pages may be incomplete. **Result: 2
VERIFIED, 27 CORRECTION, 20 UNVERIFIABLE, 1 likely CLOSED.** Every address
must be fixed or re-verified before staging/production (ADR 0009 gate).

### Chain-level summary

| Chain | In Vienna? | Recommendation |
|---|---|---|
| FitInn | Yes (~31 branches) | Replace all 12 seed addresses — none verified correct |
| McFIT | Yes | 5 addresses unverified — cross-check studio pages manually |
| clever fit | Yes (13+) | Replace all 5 entries — official slugs differ (no erdberg/meidling/fuenfhaus; donaustadt = stadlau) |
| John Harris | Yes (8) | Fix 3 of 4 (DC Tower is Wiedner Guertel 9, 1100, district 10 — not 22) |
| Holmes Place | Yes (3) | Fix 2 of 3 (club names Boerseplatz / Huetteldorf) |
| Fit Fabrik | Yes (7) | Replace all 5 entries — real sites: Hietzing, Messecarree, Maculangasse, Doebling, Handelskai, Huetteldorf, Stadlau |
| Kieser Training | Redirects to kieser.com | 3 entries unverified |
| Mrs.Sporty | Plausible | 4 entries unverified |
| Club Danube | Site unreachable | 4 entries unverified — verify before import |
| MYGYM | Plausible | 2 entries unverified |

### Studio verification table

| Studio | Status | Notes / Source |
|---|---|---|
| FitInn Landstrasser Hauptstrasse | CORRECTION | Real: Dietrichgasse 25, 1030 (fitinn.at/fitnessstudios/wien-3-dietrichgasse) |
| FitInn Margaretenstrasse | CORRECTION | Real: Hartmanngasse 1, 1050 (wien-5-hartmanngasse) |
| FitInn Mariahilfer Strasse | CORRECTION | Real: Hofmuehlgasse, 1060 (wien-6-hofmuehlgasse) |
| FitInn Alser Strasse | CORRECTION | Street/district ok; house number 28 unverified (wien-9-alser-strasse) |
| FitInn Favoritenstrasse | CORRECTION | Street/district ok; number 86 unverified (wien-10-favoritenstrasse) |
| FitInn Simmeringer Hauptstrasse | CORRECTION | Real: Gasometer complex, 1110 (wien-11-gasometer) |
| FitInn Meidlinger Hauptstrasse | CORRECTION | Real: Edelsinnstrasse, 1120 (wien-12-edelsinnstrasse) |
| FitInn Huetteldorfer Strasse | CORRECTION | Real: P+R Huetteldorf, 1140 (wien-14-huetteldorf) |
| FitInn Mariahilfer Strasse West | CORRECTION | Real: Johnstrasse, 1150 (wien-15-johnstrasse) |
| FitInn Thaliastrasse | CORRECTION | Real: Kendlerstrasse, 1160 (wien-16-kendlerstrasse) |
| FitInn Bruenner Strasse | CORRECTION | Real: Franz-Jonas-Platz, 1210 (wien-21-floridsdorf) |
| FitInn Donaufelder Strasse | CORRECTION | Real: Wagramer Strasse/Donau-Zentrum, 1220 (wien-22-donau-zentrum) |
| McFIT Wien Favoriten | UNVERIFIABLE | Davidgasse 90, 1100 — studio pages 404 (mcfit.com/at) |
| McFIT Wien Landstrasse | UNVERIFIABLE | Franzosengraben 12, 1030 |
| McFIT Wien Ottakring | UNVERIFIABLE | Thaliastrasse 125, 1160 |
| McFIT Wien Floridsdorf | UNVERIFIABLE | Bruenner Strasse 25, 1210 |
| McFIT Wien Donaustadt | UNVERIFIABLE | Stadlauer Strasse 41, 1220 |
| clever fit Wien Erdberg | CORRECTION | Official district-3 slug: wien-landstrasse; address unconfirmed |
| clever fit Wien Meidling | CORRECTION | No wien-meidling in official sitemap |
| clever fit Wien Fuenfhaus | CORRECTION | No wien-fuenfhaus; nearest official: wien-penzing / wien-mariahilfer-strasse |
| clever fit Wien Floridsdorf | CORRECTION | Slug ok; "Am Spitz 2" unverified |
| clever fit Wien Donaustadt | CORRECTION | Official slug: wien-stadlau; Wagramer Strasse 94 unconfirmed |
| John Harris Nibelungengasse | VERIFIED | Nibelungengasse 5, 1010 (johnharris.at) |
| John Harris Margareten | CORRECTION | Address ok; official club name "Schillerplatz" |
| John Harris DC Tower | CORRECTION | Real DC-Tower club: Wiedner Guertel 9, 1100, district 10 |
| John Harris Schillerplatz | CORRECTION | No club at Schillerplatz 4, 1010; JH "Schillerplatz" = Strobachgasse 7-9, 1050 |
| Holmes Place Boersegasse | CORRECTION | Official club name "Boerseplatz"; address plausible, unconfirmed |
| Holmes Place Millennium City | VERIFIED | Handelskai 94-96, 1200 (holmesplace.at) |
| Holmes Place Hietzing | CORRECTION | Official club name "Huetteldorf"; district 13/14 border to re-check |
| Fit Fabrik Schlachthausgasse | CORRECTION | Not in official sitemap; see chain row for real sites |
| Fit Fabrik Favoriten | CORRECTION | Not in official sitemap |
| Fit Fabrik Wienerberg | CORRECTION | Not in official sitemap |
| Fit Fabrik Floridsdorf | CORRECTION | Not in official sitemap |
| Fit Fabrik Kagran | CORRECTION | Nearest official: Fit Fabrik Plus Stadlau (1220) |
| Kieser Training Wien Innere Stadt | UNVERIFIABLE | kieser-training.at redirects; Kaerntner Ring 5-7 unverified |
| Kieser Training Wien Alsergrund | UNVERIFIABLE | Nussdorfer Strasse 4 unverified |
| Kieser Training Wien Hietzing | UNVERIFIABLE | Lainzer Strasse 2 unverified |
| Mrs.Sporty Wien Leopoldstadt | UNVERIFIABLE | Taborstrasse 24 unverified |
| Mrs.Sporty Wien Alsergrund | UNVERIFIABLE | Waehringer Strasse 59 unverified |
| Mrs.Sporty Wien Penzing | UNVERIFIABLE | Linzer Strasse 129 unverified |
| Mrs.Sporty Wien Liesing | UNVERIFIABLE | Breitenfurter Strasse 372 unverified |
| Club Danube Erdberg | UNVERIFIABLE | clubdanube.at timeout; Franzosengraben 2 unverified |
| Club Danube Ottakring | UNVERIFIABLE | Sandleitengasse 39 unverified |
| Club Danube Donauzentrum | UNVERIFIABLE | Wagramer Strasse 81 unverified |
| Club Danube Alterlaa | UNVERIFIABLE | Anton-Baumgartner-Strasse 44 unverified |
| MYGYM Lugner City | UNVERIFIABLE | Gablenzgasse 11 unverified |
| MYGYM Hauptbahnhof | UNVERIFIABLE | Canettistrasse 1 unverified |
| CrossFit Vienna | UNVERIFIABLE | Wehlistrasse 150, 1020 unverified |
| Doorbreaker Gasometer | UNVERIFIABLE/CLOSED | doorbreaker.at DNS dead — likely closed; recommend removal |
| Trainingslager Wien | UNVERIFIABLE | Kaiserstrasse 43, 1070 unverified |
