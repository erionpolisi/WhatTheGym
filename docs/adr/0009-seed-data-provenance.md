# ADR 0009: Seed data provenance and demo data policy

Status: accepted — 2026-08-21

## Context

CONSTRAINTS.md requires seeding real official Vienna gym data (~50 studios)
and demo reviews/cases only in local/development. Official street addresses
could not be verified against live sources during implementation.

## Decision

- The catalogue seed (`ViennaCatalog`) contains 50 real, publicly known Vienna
  studios of 10 real chains plus independents with correct district placement;
  street addresses are best-effort and **flagged for verification** in
  docs/seed-data.md before any staging/production use.
- Seeding is idempotent (slug-keyed) and deterministic (fixed timestamps); it
  never overwrites admin edits.
- Demo users/reviews/cases are created only when `Seed:SeedDemoData=true` AND
  the runtime environment is Development — the flag is ignored elsewhere, so
  staging/production can never receive demo content even by misconfiguration.
- Legal document drafts (v1, `ENTWURF` marker) are seeded in all environments
  so the public legal endpoints are always functional.

## Consequences

Local and CI environments get a realistic, stable catalogue; production data
quality has an explicit verification gate and a correction flow
(ContactRequest → admin).
