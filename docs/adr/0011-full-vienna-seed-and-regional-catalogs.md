# ADR 0011: Full Vienna seed and inert regional catalogs

Status: accepted — 2026-08-31 (owner decision)

## Context

CONSTRAINTS.md originally capped the seed at "approximately 50 studios" and
scoped all data to Vienna. The owner decided: (a) the cap is unnecessary —
the Vienna seed should contain every studio verifiable against official
sources; (b) as preparation for future expansion, every other Austrian
Bundesland gets its own catalog of researched gyms — as pure reference data,
explicitly NOT integrated.

## Decision

- **Vienna**: `ViennaCatalog` is rebuilt from a 2026-08-31 web research pass
  (official chain studio finders, sitemaps, WKO where fetchable). Every entry
  carries a confidence level in docs/seed-data.md. The former ~50 cap is
  removed from CONSTRAINTS.md.
- **Regional catalogs**: eight static classes under
  `Gym.Infrastructure/Seeding/Regional/` (Burgenland, Carinthia, LowerAustria,
  UpperAustria, Salzburg, Styria, Tyrol, Vorarlberg) hold `RegionalSeedGym`
  records (city-based, since the domain's district model is Vienna-only).
  They are compiled (so they cannot rot silently) but are referenced by
  nothing: no seeder, no tests, no API. Wiring them up is a future, separate
  decision that will also need domain changes (region/city on Gym).
- **SOLID cleanup**: chains and amenities moved from `ViennaCatalog` into
  `SeedVocabulary` (single source of truth — they are platform-wide, not
  Vienna-specific). Seed record types moved to `SeedModels.cs`. Catalogs now
  hold exactly one thing: their region's gym list (SRP).
- **Drift expectation**: gym data ages (openings/closures). Catalogs carry
  the research date; docs/seed-data.md remains the verification ledger and
  re-verification before production import stays mandatory (ADR 0009).

## Consequences

- The local/dev database now seeds a considerably larger, corrected Vienna
  catalog; seeding stays slug-keyed and idempotent, admin edits are never
  overwritten.
- Regional data ships in the binary without any runtime effect; the unused-
  data cost is a few KB and zero behavior.
- Tests that relied on specific old seed entries were updated together with
  the catalog rewrite.
