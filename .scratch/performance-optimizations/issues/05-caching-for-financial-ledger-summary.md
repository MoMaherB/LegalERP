# 05: Caching for Financial Ledger Summary

**What to build:** Implement an intelligent in-memory cache with sliding-window expiration and proactive invalidation for the firm-wide financial summary endpoint (`GET /api/financials/summary`), avoiding costly full-table aggregations on every request.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] `GET /api/financials/summary` serves aggregated ledger totals from `IMemoryCache` with a configurable sliding expiration (e.g. 60 seconds) when no underlying financial changes occurred.
- [ ] Financial cache is proactively invalidated whenever a case or company's agreed fee, payment, or financial transaction is created, updated, or deleted.
- [ ] Appropriate HTTP caching response headers (`Cache-Control`, `ETag` or `Vary`) are emitted so browsers and clients avoid redundant transfers.
- [ ] Automated tests verify cache hits, cache misses, and cache eviction upon transaction changes.
