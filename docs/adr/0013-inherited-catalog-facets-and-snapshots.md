# ADR 0013: Inherited catalog facets and explicit snapshots

Status: accepted - 2026-10-14 (owner decision)

## Context

Scores and reviews remain WhatTheGym's comparison core, but users also need
stable facts for narrowing the catalogue. Prices and offers change too quickly
to maintain reliably. Repeating the same facts on every chain location would
create avoidable drift. Database edits also need a reviewable path back to JSON
without creating two automatically synchronized sources of truth.

Community Q&A is a useful future complement for variable, location-specific
details, but it requires its own moderation, reporting, retention, and privacy
design and is outside this data-focused change.

## Decision

- Store controlled training types as flags and target audience as `General` or
  `WomenOnly`.
- A chain provides defaults. A location's nullable value means inherit; a set
  value replaces the chain value completely. Public responses return effective
  values, while administration retains raw defaults and overrides.
- Future filters and recommendations must be deterministic and based on
  controlled catalogue facets and existing scores. No AI inference is added.
- Membership prices remain excluded from storage, display, filtering, and
  ranking.
- `catalog.json` remains the versioned seed source. CatalogCli's explicit
  `export-db` command writes an atomic, deterministic `catalog-DB.json`
  snapshot. It never overwrites the seed source and excludes IDs, timestamps,
  reviews, personal data, and secrets.

## Consequences

- Common chain data is maintained once, while exceptional locations remain
  representable without free-form tags.
- The API can expose useful filter foundations before filter UI/query behavior
  is implemented.
- Import and export are deliberately asymmetric maintenance operations, so a
  human reviews differences before changing the seed source.
- Q&A needs a separate ADR before implementation.
