# ADR 0008: Azure cost plan (<= 10 EUR/month target)

Status: accepted — 2026-08-21

## Context

Ongoing production cost is capped at 10 EUR/month. The smallest managed Azure
PostgreSQL (Flexible Server B1ms + 32 GB storage) alone costs ~14–17 EUR/month,
which already exceeds the cap.

## Decision

Two prepared variants in `infrastructure/azure/main.bicep`:

1. **Default (within cap): hybrid** — Azure Static Web Apps Free (frontend),
   Azure Container Apps consumption with scale-to-zero (API, ~0–5 EUR),
   Key Vault + capped Log Analytics (~0–2 EUR), and an **external managed
   PostgreSQL free tier** (e.g. Neon/Supabase; connection string stored in Key
   Vault). Estimated total: **~0–7 EUR/month**.
2. **All-Azure (over cap, documented deviation)** — same, plus Azure Database
   for PostgreSQL Flexible Server B1ms via `deployPostgres=true`. Estimated
   total: **~16–22 EUR/month**.

The MVP goes live on variant 1 to honor the cap; variant 2 is the upgrade path
once traffic or compliance justifies it (single parameter flip + data
migration).

## Tradeoffs

- Variant 1 places the database outside Azure: latency (choose an EU region),
  a second vendor, and free-tier limits (compute autosuspend, storage caps) —
  acceptable for MVP traffic; the app is provider-agnostic (plain PostgreSQL,
  no proprietary extensions beyond `pg_trgm`).
- Scale-to-zero causes cold starts (~seconds) — acceptable for MVP.
- Costs re-evaluated after launch; this ADR must be revised before variant 2.

## Addendum (2026-09-01): ghcr.io instead of Azure Container Registry

Images are published to **GitHub Container Registry** (free for public
packages, generous free tier for private) instead of ACR Basic (~5 EUR/month).
CI already builds on GitHub; pushing with the built-in `GITHUB_TOKEN` needs no
extra credentials. If the package stays private, the container app needs a
`registries` entry (username + PAT secret) in Bicep. Tradeoff: image pulls
cross clouds on cold start — negligible for a ~100 MB image at MVP traffic.

Estimated monthly cost (fill with billed numbers after the first staging
month, before any production go-live):

| Item | SKU | Est. EUR/month |
| --- | --- | --- |
| Container Apps (scale-to-zero, low traffic) | Consumption | 0–3 |
| Static Web Apps | Free | 0 |
| PostgreSQL (external free tier, e.g. Neon) | Free | 0 |
| Container registry | ghcr.io | 0 |
| Key Vault | Standard, few ops | <1 |
| Log Analytics (capped 0.1 GB/day, 30 d retention) | PerGB2018 | 0–2 |
| App Insights (availability tests only, no SDK) | — | 0–1 |
| DNS zone | — | ~0.4 |
| Domain (yearly / 12) | — | ~2 |
| **Total** | | **~3–9** |
