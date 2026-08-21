# Domain model

All timestamps are UTC (`timestamptz`); date-based rules (case-number year)
use `Europe/Vienna`. Country code is `AT`.

## Catalogue

- **GymChain** — name, stable unique slug, optional website.
- **GymEntry** (table `Gyms`) — name, stable unique SEO slug, optional chain,
  Vienna district (1–23), address, postal code (1xxx), optional website/phone/
  description, status (`Draft`, `Active`, `TemporarilyClosed`,
  `PermanentlyClosed`), amenity ids (uuid[]), optional official opening hours.
  - Public visibility: every status except `Draft`.
  - Reviews accepted: `Active` and `TemporarilyClosed` only.
- **Amenity** — name + slug; assignment via `GymEntry.AmenityIds`.

## Users

- **User** — Google subject (unique), email, `EmailVerified`, display name,
  role (`User`, `Moderator`, `Admin`), status (`Active`, `Deleted`).
  `email_verified=true` yields the "Verifiziert ueber Google" badge — explicitly
  not proof of gym membership. Account deletion anonymizes all identifying
  fields (tombstone values) and keeps the row for referential integrity.
- **RefreshToken** — SHA-256 hash only, expiry, revocation, `ReplacedByTokenHash`
  chain for reuse detection.

## Reviews and scoring

- **Review** — gym + author, 11 optional 1–5 category ratings (at least one
  required), optional sanitized text (≤ 4000 chars), status:
  - `Published` (default; automatic publication),
  - `SoftDeleted` (reversible; by author, moderator, admin or account deletion;
    with origin + reason),
  - `UnderReview` (fast-track legal hiding; temporary),
  - `RemovedLegal` (result of a `FullyRemoved` decision; only an upheld appeal
    reinstates it).
  Only `Published` reviews are public and score-relevant.
- **ReviewRevision** — immutable snapshot of text + ratings before each edit;
  retained 3 years after the review leaves publication (configurable, legal
  holds pause deletion).
- **GymRatingSummary** — materialized aggregate per gym: total/area scores,
  `ScoreBasis`, per-category averages/counts (jsonb), review count.
  See [scoring.md](scoring.md).

## Legal

- **LegalCase** — one public report path per review. Human-readable case number
  (`WTG-<year>-<seq>`), category, reporter contact data, description, status
  (`Received` → `UnderReview` → `Decided` → `Closed`), classification
  (`Unclassified`, `Normal`, `FastTrackObviouslyIllegal`), decision
  (`KeepOnline` | `FullyRemoved`) with mandatory rationale, hashed status and
  appeal tokens, appeal deadline (≥ 6 months after decision).
- **LegalCaseEvent** — append-only audit trail (unique sequence per case,
  UPDATE blocked by a database trigger). Notification texts are stored verbatim.
- **LegalCaseAppeal** — tokenized appeal with outcome (`DecisionUpheld`,
  `DecisionReversed`) and rationale; reversal flips the review state.
- **LegalHold** — pauses retention deletion for a case, review, or user.
- **LegalDocument** — versioned imprint/privacy/terms; latest published version
  is active; all seeded content is marked `ENTWURF - anwaltlich pruefen lassen`.

## Communication and telemetry

- **ContactRequest** — public form (general, gym suggestion, data correction),
  admin-managed status. Only Admins create gyms directly.
- **OutboxEmail** — persistent transactional outbox for Resend with retry and
  exponential backoff (8 attempts max).
- **AnalyticsEvent** — PII-free: allowlisted event type, query-stripped path,
  short-lived rotating hashed session bucket. No IP, no fingerprinting.
