# Legal, privacy, and moderation

All legal copy in the product is a draft: `ENTWURF - anwaltlich pruefen lassen`.

## Review reports (single path)

There is exactly one public report flow: `POST /api/v1/reviews/{id}/report`
creates a **LegalCase**. There is no `ReviewReport` entity, no partial
redaction, and no `ReviewRedaction`.

- The reporter receives a case number (`WTG-<year>-<seq>`) and a confidential
  status token (stored hashed; shown exactly once).
- Reported content **stays online** while a normal report is reviewed.
- Only an explicit staff classification as `FastTrackObviouslyIllegal` hides
  the review (`UnderReview`) before the decision; the author is notified.

## Case lifecycle

```
Received ──► UnderReview ──► Decided ──► Closed
    │  (classify: Normal | FastTrackObviouslyIllegal)
    └─ fast-track may hide the review immediately
```

Correcting a fast-track misclassification back to `Normal` republishes the
hidden review automatically (unless another open fast-track case for the same
review exists); the release is audited as `ContentRestored`.

- Decisions: `KeepOnline` or `FullyRemoved` — nothing else. `FullyRemoved`
  sets the review to `RemovedLegal`. Every decision requires a documented
  rationale.
- Every step appends an immutable `LegalCaseEvent` (unique sequence per case;
  a PostgreSQL trigger blocks UPDATE). Notification texts are stored verbatim
  in the audit trail **except confidential tokens**: status/appeal links are
  masked as `***` in the audit copy so staff with case access can never reuse
  them (ADR 0012). The mail itself carries the real link.
- Notifications go through the persistent outbox (Resend or dev logging):
  report received (reporter), content hidden (author, fast-track only),
  decision (reporter + author), appeal received, appeal decided.

## Appeals

- The appeal token goes to the adversely affected party (author on
  `FullyRemoved`, reporter on `KeepOnline`) inside the decision mail.
- Appeals stay open at least six months after the decision
  (`LegalCase.AppealWindow` = 183 days).
- Outcomes: `DecisionUpheld` or `DecisionReversed`. A reversal reinstates a
  removed review (or removes a kept one) and is fully audited.

## Retention and legal holds

Configured via `Retention` options; the daily `RetentionSweeper` enforces:

| Data                          | Default retention                          |
| ----------------------------- | ------------------------------------------ |
| Legal case + audit events     | 7 years after case closure                 |
| Review revisions              | 3 years after the review left publication  |
| Raw analytics events          | 400 days                                   |
| Sent/failed outbox mails      | 90 days                                    |

Active **LegalHolds** (case-, review-, or user-scoped) pause deletion
unconditionally: a review-scoped hold protects the review's revisions, a
user-scoped hold protects all revisions of the user's reviews, and case purges
are blocked by holds on the case, the reported review, or the review author.
Deleted/under-review content is never public and never score-relevant but
remains archived under these rules.

## Privacy (GDPR)

- Every value logically linkable to a person is treated as personal data; the
  processing-activities record (`GET /api/v1/legal/processing-activities`,
  [processing-activities.md](processing-activities.md)) is generated from code
  and enforced by tests.
- `GET /api/v1/me/export` — complete personal data export (account, reviews,
  revisions, own reports, contact requests).
- `DELETE /api/v1/me` — anonymizes the account (tombstone values), soft deletes
  own reviews (origin `AccountDeletion`), revokes refresh tokens; reviews under
  an active legal hold stay archived and restricted until the hold is released.
- Public endpoints never expose email addresses, case contents, tokens, or
  archived review data. Case status is only accessible with the hashed token.
- Transparency report: `GET /api/v1/legal/transparency-report?year=` returns
  aggregate, PII-free counts.

## Anti-abuse (MVP scope)

Rate limiting (per-IP, in-memory), honeypot fields on public forms (silently
dropped), server-side checks (length bounds, link-count limits, sanitization).
No CAPTCHA, no external anti-abuse vendors.
