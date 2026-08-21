# Scoring rules

Implemented in `Gym.Domain.Scoring.ScoreCalculator`; enforced by unit and
integration tests. Only **published** reviews count.

## Categories

Membership area (4 direct 1–5 ratings):
`PriceValue`, `ContractTerms`, `Billing`, `CancellationExperience`

Studio area (7 direct 1–5 ratings):
`Equipment`, `Cleanliness`, `Staff`, `Crowding`, `ChangingRoom`, `Showers`, `Atmosphere`

A review must contain at least one direct rating; every provided value is 1–5.
Missing values are `null` — never zero, never imputed.

## Aggregation

1. **Category average** — mean over all published reviews that rated that
   category. A category without data is `null` with `ratingCount = 0`.
2. **Area score** — mean of the *category averages* available in that area
   (not the raw rating mean, so sparsely rated categories are not drowned out).
   An area without any rated category is `null`.
3. **Total score** —
   - both areas available: `(membership + studio) / 2` → `scoreBasis = "both"`,
   - only one area: total = that area → `"membershipOnly"` / `"studioOnly"`,
   - no data: `null` → `"none"`.
4. Rounding: 2 decimals, `MidpointRounding.AwayFromZero`, applied at the end.

## API contract

`GET /api/v1/gyms/{slug}/summary` (and embedded in the gym detail):

```json
{
  "totalScore": 3.25,
  "membershipScore": 2.0,
  "studioScore": 4.5,
  "scoreBasis": "both",
  "reviewCount": 1,
  "categories": [
    { "category": "priceValue", "area": "membership", "average": 2.0, "ratingCount": 1 },
    { "category": "equipment", "area": "studio", "average": 5.0, "ratingCount": 1 }
  ]
}
```

All 11 categories are always present in `categories`.

## Materialization

Summaries are stored in `GymRatingSummaries` and recomputed in the same unit of
work whenever review state changes (create, edit, soft delete, restore, legal
hide/remove/reinstate, account deletion). `POST /api/v1/admin/summaries/rebuild`
recomputes every gym (admin only).
