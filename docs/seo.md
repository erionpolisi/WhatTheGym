# SEO

The Next.js frontend is a thin, SEO-aware consumer of the API.

## Rendering strategy

- Public pages (home, `/studios`, `/studios/[slug]`, legal pages, transparency)
  are server-rendered with incremental revalidation (30–3600 s depending on
  volatility). Forms and account features are client components.
- When the API is unreachable at build time, pages degrade gracefully and ISR
  fills content on the first request.

## Metadata

- `metadataBase` + per-page titles/descriptions (German), canonical URLs on gym
  detail pages, Open Graph defaults (`de_AT`).
- `app/sitemap.ts` generates the sitemap from static routes plus all public gym
  slugs; `app/robots.ts` allows crawling except `/konto` and tokenized legal
  pages (`/rechtliches/fall/`, `/rechtliches/einspruch/`), which are also
  `noindex`.

## Structured data

Gym detail pages embed schema.org JSON-LD:

- `@type: ExerciseGym` with `PostalAddress` (Wien/AT),
- `AggregateRating` (1–5, real `ratingCount`) only when a total score exists —
  never fabricated from missing data.

## Slugs

Slugs are stable, ASCII, German-transliterated (`ä→ae`, `ß→ss`), unique with
numeric suffixes, and never change on rename (the slug is generated once at
creation). URLs: `/studios/<slug>`.
