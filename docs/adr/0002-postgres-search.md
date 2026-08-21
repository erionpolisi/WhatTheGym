# ADR 0002: PostgreSQL full-text + trigram search, no external search service

Status: accepted — 2026-08-21

## Context

Search covers ~50–500 Vienna gyms with filters (district, scores, chain) and a
free-text term. CONSTRAINTS.md forbids external search services and PostGIS.

## Decision

A database-generated `tsvector` column (`german` configuration over name and
address) with a GIN index, plus a `pg_trgm` GIN index on the name for fuzzy
matching, plus a chain-name ILIKE fallback. One parameterized raw SQL query
joins the materialized rating summaries for score filtering/sorting and ranks
by `ts_rank + similarity`. The Application layer sees only
`IGymSearchQuery`/`ISearchIndex`; the PostgreSQL `ISearchIndex` is a no-op
because the database maintains the index itself.

## Consequences

- Zero additional infrastructure or cost; index maintenance is automatic.
- German stemming plus typo tolerance covers the expected catalogue size by a
  wide margin; a future external index can implement the same ports.
- Raw SQL is confined to one Infrastructure class and covered by integration
  tests (seeded FitInn search, filters, sorting).
