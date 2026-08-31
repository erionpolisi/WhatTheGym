# ADR 0010: Frontend design system (dark minimalism, single accent)

Status: accepted — 2026-08-31

## Context

The MVP frontend was functionally complete but visually generic (default
blue links, light theme, table-based score display). Requirements for the
redesign: responsive, high contrast, catchy, minimalist and professional
(explicitly "not colorful"), with large, prominent rating scores. Candidate
directions were evaluated against current UI trends (Barely There UI,
Controlled Maximalism, Human Touch, Grade-School Colors, blueprint styles,
heavy animation, nostalgia, sound, tech-gradient).

## Decision

- **Direction**: "Barely There" dark minimalism as the foundation
  (near-black surfaces, generous whitespace, thin borders) combined with
  controlled-maximalist typography — oversized display headlines and giant
  score numerals are the only "loud" elements.
- **Color**: strict monochrome dark palette plus exactly one accent,
  warm orange (`#ff5c1f`), used for scores, primary actions, and focus
  states. Success/danger exist only as functional colors. All text/background
  pairs meet WCAG AA (accent on background ≈ 6.3:1).
- **Typography**: `Space Grotesk Variable` for display/headings/numerals,
  `Inter Variable` for body text. Delivered as self-hosted Fontsource npm
  packages — no external font requests at build or runtime (GDPR-safe,
  proxy/CI-safe, no layout dependency on Google). Score values use
  `font-variant-numeric: tabular-nums`.
- **Score presentation**: gym detail shows a giant total numeral
  (`clamp(4rem…6.5rem)`) with area split (Mitgliedschaft/Studio) beside it;
  categories render as horizontal fill bars with numeral + rating count.
  List cards show a compact large-numeral score chip. Missing data renders
  as "–"/"keine Daten", never as zero (CONSTRAINTS scoring rules).
- **Implementation**: plain CSS custom properties in `globals.css` (design
  tokens for color/spacing/type). **No Tailwind, no CSS-in-JS, no component
  library, no animation library** — micro-interactions are CSS-only and
  guarded by `prefers-reduced-motion`. `color-scheme: dark` keeps native
  form controls consistent.
- **Rejected trends**: hand-drawn/nostalgia/sound (undermine trust and
  accessibility for a review platform), purple-blue-teal gradients (colorful
  and generic), full blueprint aesthetic (kept only tabular numerals).
- **Accessibility**: skip-link, visible `:focus-visible` rings, aria-labels
  on score bars, semantic nav landmarks, fluid `clamp()` type, mobile-first
  layout without JS (no burger menu; the nav wraps).

## Consequences

- Zero new runtime dependencies; two build-time font packages
  (`@fontsource-variable/inter`, `@fontsource-variable/space-grotesk`).
- The design system lives in one file (`frontend/app/globals.css`); future
  pages/components must use the existing tokens and utility classes instead
  of introducing local styles.
- Dark-only for the MVP (no theme toggle); a light theme would be a token
  swap later because all colors are custom properties.
- Score bars require `average/5` width math in components; the visual
  contract (numeral + bar + count) is defined once in `components/Scores.tsx`.
