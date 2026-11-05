# ADR 0014: Chain-first directory and rule-based gym recommendations

Status: accepted - 2026-10-15 (owner decision)

## Context

The studio directory listed all 113 Vienna locations as a flat list with a
chain dropdown. The owner wants the directory organized by chain: a search hit
first shows the matching chains with their hit counts, and a click drills into
the locations while keeping the active filters.

Additionally, users should be able to answer a short series of questions
(gender, women-only studio, ranked districts, training types, amenities,
score importance) and receive the best matching gyms. ADR 0013 already fixed
the ground rules: recommendations must be deterministic, based on controlled
catalogue facets and existing scores, without AI inference and without prices.

## Decision

### Chain-first directory

- New endpoint `GET /api/v1/chains/search` groups the published catalogue by
  chain using the same filter semantics as `/gyms` (`term`, `district`,
  `minTotalScore`). Each item returns the chain, its matching gym count, and
  the average of the available location total scores.
- Chains without a matching gym are not returned (inner join by design). The
  sort order is average score descending (nulls last), then gym count, then
  name, so rated chains surface first without hiding unrated ones.
- Catalogue invariant: every gym has a `ChainSlug`. Independent studios get
  their own single-gym chain (this change added "CrossFit Vienna" as chain
  number 24 for the previously chainless "The Starship" location).
- The frontend `/studios` page shows the chain directory by default and the
  gym list only with `?chain=<slug>`; the back link and the drill-down link
  carry `term`, `district`, and `minTotalScore` along. The chain filter
  dropdown is removed.

### Rule-based recommendations

- New endpoint `POST /api/v1/gyms/recommendations`. POST (not GET) because the
  request carries a structured body including an ordered district ranking;
  there is no caching or deep-linking requirement for results.
- The computation is stateless: preferences are validated, scored in memory
  against one catalogue query, and never persisted. No personal data is
  stored; `gender` is only used to filter women-only studios.
- Hard filters before scoring: men never receive women-only studios;
  `womenOnlyRequired` keeps only women-only studios (combined with
  `gender=male` it is a validation error); selected districts are hard
  filters; only `Active` and `TemporarilyClosed` gyms are considered.
- Soft criteria contribute weight points; only criteria the user actually
  provided count toward the applicable total:
  - Districts: 30. A ranked list earns `30 * (n - rank) / n`.
  - Training types: 25, proportional to matched requested types.
  - Amenities: 20, proportional to matched requested amenities.
  - Community score: weight by stated importance (`high` 35, `medium` 25 =
    default, `low` 10), normalized as `(totalScore - 1) / 4`.
  - A missing community score earns the neutral midpoint (0.5 x weight) and a
    German note - missing data is never treated as zero (see scoring rules).
  - `TemporarilyClosed` multiplies the earned points by 0.6 and adds a note.
  - `matchPercent = round(100 * earned / applicable)`, clamped to 0-100.
- The response returns the top 3 by default (limit 1-10) with German
  (`de-AT`) reasons and notes explaining every match.
- The weights are product decisions pinned by unit tests
  (`GymRecommendationTests`); changing them is a conscious edit, not a
  refactoring side effect.
- Frontend: the hero button "Probier es aus" leads to `/empfehlung`, a
  step-by-step wizard over exactly these facets with a skip link to a
  one-page form (`?modus=formular`). Districts are selected in priority
  order and shown with rank badges.

## Consequences

- The directory stays navigable as the catalogue grows: 23 chains instead of
  113 cards, with counts that reflect the active filters.
- Recommendations are deterministic, explainable, and cheap (one catalogue
  query, in-memory scoring); identical input yields identical output.
- New catalogue facets later (e.g. opening-hours coverage) require revisiting
  the weight table deliberately.
- `POST` responses are not cacheable; acceptable because the computation is
  in-memory over a few hundred rows.
- Rate limiting applies as to other public endpoints; the body is validated
  and size-capped (districts, training types, amenities lists are trimmed and
  deduplicated server-side).
