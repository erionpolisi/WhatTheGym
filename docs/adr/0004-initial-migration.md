# ADR 0004: Single squashed initial migration for the greenfield MVP

Status: accepted — 2026-08-21

## Context

The schema was designed as a whole for the MVP; there is no production data to
migrate incrementally yet.

## Decision

One `InitialCreate` migration contains the full schema plus hand-written SQL:
the generated `tsvector` column and GIN indexes, the `pg_trgm` extension, the
`legal_case_seq` sequence, and the UPDATE-blocking trigger on
`LegalCaseEvents`. From now on every schema change gets its own migration; CI
enforces this via `dotnet ef migrations has-pending-model-changes` and by
applying all migrations to a fresh PostgreSQL.

## Consequences

- Clean starting point, no migration archaeology.
- Custom SQL lives in the migration (and its `Down`), keeping
  `dotnet ef database update` the single source of schema truth.
