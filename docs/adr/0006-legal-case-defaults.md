# ADR 0006: Legal case implementation defaults

Status: accepted — 2026-08-21

## Context

CONSTRAINTS.md defines the legal-case product rules; several implementation
details were open and are reversible, so conservative defaults were chosen.

## Decision

- **Case numbers** use one global PostgreSQL sequence formatted as
  `WTG-<Vienna-year>-<seq:D6>`. Sequences are unique and human-readable; they
  are not per-year contiguous (acceptable, documented).
- **Tokens** (status + appeal) are 256-bit random values, stored only as
  SHA-256 hashes, shown/sent exactly once.
- **Appeal token recipient** is the adversely affected party: the review author
  for `FullyRemoved`, the reporter for `KeepOnline`.
- **Author notification** happens when their content is affected (fast-track
  hiding, decision); a rejected normal report does not notify the author.
- **Audit immutability**: `LegalCaseEvents` get a unique `(case, sequence)`
  index and a database trigger that rejects UPDATE. DELETE stays possible
  exclusively for retention-driven cleanup after the 7-year period (holds
  pause it); the application never deletes events otherwise.
- **Fast-track** hiding is an explicit staff classification
  (`FastTrackObviouslyIllegal`), never automatic.
- Exact notification texts (German drafts, `ENTWURF` marker) are stored
  verbatim in `NotificationQueued` audit events.

## Consequences

Full lifecycle covered by integration tests (report → status token → classify
→ decide → appeal → reversal → close → export → transparency counts).

## Amendment (2026-09-01): superseded in part by ADR 0012

- Notification texts in audit events are no longer fully verbatim: the
  confidential status/appeal token inside links is masked as `***` (the
  outbox mail keeps the real link). Rationale: audit events are readable by
  staff with case access and must not contain usable secrets.
- Reclassifying a fast-track case back to `Normal` republishes the hidden
  review (audited as `ContentRestored`) unless another open fast-track case
  for the same review exists.
