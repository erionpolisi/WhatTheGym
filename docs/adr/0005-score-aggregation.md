# ADR 0005: Score aggregation semantics

Status: accepted — 2026-08-21

## Context

CONSTRAINTS.md fixes the outer rules: aggregate only available data, 50/50
total when both areas exist, expose `scoreBasis`, missing data is null. The
inner details needed a decision.

## Decision

- An **area score is the mean of its category averages** (categories without
  data are skipped), not the mean of all raw ratings. This keeps a single
  heavily-rated category from dominating an area.
- Rounding: 2 decimals, `MidpointRounding.AwayFromZero`, applied only at the
  edges (stored/exposed values); intermediate math stays unrounded.
- Score responses always contain all 11 categories with `average` (nullable)
  and `ratingCount`, both area scores, the total, `scoreBasis`
  (`both|membershipOnly|studioOnly|none`) and the published-review count.
- Summaries are **materialized** (`GymRatingSummaries`) and recomputed
  synchronously in the same unit of work as the review change — strong
  consistency at MVP scale beats eventual-consistency machinery. An admin
  rebuild command exists for repair.
- One active (published/under-review) review per user and gym; a new review
  may be written after the previous one was soft deleted.

## Consequences

Deterministic, unit-tested semantics (domain tests cover area averaging,
rounding, null propagation and basis selection; integration tests verify the
HTTP contract end to end).
