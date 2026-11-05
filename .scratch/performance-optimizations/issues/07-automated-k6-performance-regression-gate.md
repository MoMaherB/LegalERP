# 07: Automated k6 Performance Regression Gate in Test Pipeline

**What to build:** Integrate an automated k6 performance regression gate script into the solution tooling, asserting that p95 response time remains below 250ms and error rates remain at 0% across all updated paginated and cached endpoints.

**Blocked by:** 01 (Core Pagination & Cases Pagination), 02 (Companies Pagination), 03 (Clients Pagination), 04 (PostgreSQL Indexes), 05 (Financials Caching), 06 (Rate Limiting)

**Status:** ready-for-agent

- [ ] A dedicated benchmark script in `.scratch/k6-performance/` validates paginated cases, companies, and clients endpoints, cached financials summary, and rate-limited auth.
- [ ] A runnable CLI command or script (e.g., `run-performance-gate.ps1`) executes the benchmark, verifies thresholds, and fails with a non-zero exit code if any threshold is violated.
- [ ] Thresholds are strictly configured: `http_req_failed < 1%`, `http_req_duration p(95) < 250ms`, and all module health checks pass 100%.
- [ ] Documentation is added to `docs/` or `AGENTS.md` explaining how developers and CI agents run and verify the performance gate before major releases.
