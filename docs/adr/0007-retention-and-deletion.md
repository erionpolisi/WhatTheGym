# ADR 0007: Retention enforcement via daily sweeper; account deletion semantics

Status: accepted — 2026-08-21

## Context

Retention periods are configurable, legal holds must pause deletion, and
account deletion must anonymize without destroying legally required records.

## Decision

- A hosted `RetentionSweeper` runs daily and enforces: case audit data 7 years
  after closure, review revisions 3 years after the review left publication,
  analytics 400 days, sent/failed outbox mails 90 days. Rows protected by an
  active `LegalHold` are always skipped.
- **Account deletion**: refresh tokens are revoked, own published/under-review
  reviews are soft deleted (origin `AccountDeletion`), the user row is
  anonymized in place with tombstone values (no hard delete, preserving
  referential integrity of cases and revisions). Reviews under an active legal
  hold remain archived and restricted until the hold is released; case records
  keep their own reporter snapshots and stay admin-only.
- Anonymized accounts cannot log in again; a returning person gets a fresh
  account.

## Consequences

Deletion is GDPR-conservative (anonymize + restrict rather than destroy where
legal duties may exist), fully configurable, and testable — the sweeper logic
is deterministic and hold-aware.

## Amendment (2026-09-01): hold scope resolution (ADR 0012)

"Protected by an active LegalHold" is resolved across scopes, not just by the
directly linked id: revision purges skip reviews with a review- or user-scoped
hold; case purges skip cases with a hold on the case, the reported review, or
the review author. Previously user-scoped holds were not enforced by the
sweeper.
