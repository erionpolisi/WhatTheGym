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
(`WTG-2026-000001`, status `Received`). Demo data never reaches staging or
production; the flag is ignored outside Development.
