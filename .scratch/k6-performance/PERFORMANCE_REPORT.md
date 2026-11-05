# LegalERP Performance Benchmark & Diagnostic Report

**Date:** 2026-11-05  
**Tooling:** Grafana k6 v2.3.0  
**Target Environment:** Localhost (Kestrel HTTPS + PostgreSQL 18 + Hangfire)  
**Total Requests Benchmarked:** 34,307 requests across 2 benchmark suites  

---

## 1. Executive Summary

| Metric | Target SLA | Measured Result | Status |
|---|---|---|:---:|
| **Overall HTTP Duration (p95)** | < 300 ms | **6.97 ms** (Modules) / **9.13 ms** (50-VU Stress) | **EXCELLENT** |
| **Median HTTP Duration (p50)** | < 50 ms | **3.55 ms** (Modules) / **3.86 ms** (50-VU Stress) | **EXCELLENT** |
| **System Throughput** | > 100 RPS | **423.0 – 533.5 Requests/Sec** | **EXCELLENT** |
| **HTTP Error Rate (5xx/4xx)** | < 1.0% | **0.00%** (0 failed out of 23,291 requests in stress test) | **PASS** |
| **Peak Concurrency Tested** | 50 concurrent users | **6,456 User Journeys Completed** | **PASS** |

Under high concurrent load (simulating 50 simultaneous lawyers, accountants, and paralegals executing mixed read/search/write workflows), the application maintained sub-10ms p95 latencies and 0.00% error rates with zero database connection exhaustion.

---

## 2. Module-by-Module Benchmark Results (10 Concurrent VUs)

| Module | Endpoints Tested | Checks Passed | Avg Latency | p90 Latency | p95 Latency | Health Status |
|---|---|:---:|:---:|:---:|:---:|:---:|
| **1. Auth** | `GET /api/auth/me` | 100% | 13.6 ms | 8.0 ms | 45.0 ms | **Healthy** |
| **2. Cases** | `GET /api/cases`, `/api/cases/search?term=...` | 100% | 4.9 ms | 5.0 ms | 6.0 ms | **Healthy** |
| **3. Companies** | `GET /api/companies`, `/api/companies/search` | 100% | 7.9 ms | 5.0 ms | 6.0 ms | **Healthy** |
| **4. Clients (360)** | `GET /api/clients`, `/api/clients/search` | 100% | 7.5 ms | 7.0 ms | 7.9 ms | **Healthy** |
| **5. Attorneys** | `GET /api/attorneys` | 100% | 6.1 ms | 7.0 ms | 8.0 ms | **Healthy** |
| **6. Financials** | `GET /api/financials/summary` | 100% | 6.6 ms | 8.0 ms | 9.0 ms | **Healthy** |
| **7. Notifications** | `GET /api/notifications`, `/unread-count` | 100% | 4.9 ms | 5.0 ms | 5.0 ms | **Healthy** |
| **8. Users / Admin** | `GET /api/users` | 100% | 8.0 ms | 7.0 ms | 8.0 ms | **Healthy** |
| **9. Web UI (SSR)** | `GET /`, `GET /cases`, `GET /companies` | 100% | 12.8 ms | 7.0 ms | 9.0 ms | **Healthy** |

---

## 3. Whole-Website Concurrency & Logic Stress (Up to 50 Concurrent VUs)

* **Duration:** 55 seconds (staged ramp-up: 15 VUs → 35 VUs → 50 VUs → ramp-down)
* **Total Completed Journeys:** **6,456 full multi-step workflows**
* **Workflows Profiled:**
  * **Journey A (Case Manager):** Cases list + search query + web view rendering.
  * **Journey B (Corporate Specialist):** Companies list + category filtering + search query + web view rendering.
  * **Journey C (Client Intake / 360):** Client listing + fuzzy search + client web page.
  * **Journey D (Financial Auditor):** Real-time global ledger calculation + financials dashboard.
  * **Journey E (Staff & Notifications):** Polling unread count + notification feed + attorney directory.
  * **Write Traffic:** Concurrent case creation and cleanup transactions under load.

### Key Stress Metrics:
* **User Journey Latency (End-to-End Workflow):** Average **24.8 ms**, p90 **25.0 ms**, p95 **39.0 ms**.
* **Write Transaction Latency (POST new case + DB Commit):** Average **27.0 ms**, Median **10.0 ms**, p95 **105.0 ms**.
* **Total Network Throughput:** 108 MB transferred at 2.0 MB/s.

---

## 4. Production Tickets & Optimization Roadmap

Although current latency and throughput are exceptional on the active dataset, the following 5 tickets represent high-value hardening tasks to maintain this performance when the database scales to tens of thousands of records in production.

### Ticket PERF-01: Add Server-Side Pagination to Entity List Endpoints
- **Problem:** Currently, `/api/cases`, `/api/companies`, and `/api/clients` return all non-deleted entities in a single JSON payload.
- **Risk at Scale:** When the firm accumulates 5,000+ cases, payload sizes and EF Core deserialization will spike memory and network transfer.
- **Solution:** Introduce optional pagination parameters (`pageNumber`, `pageSize`, default 50) and return a `PagedResult<T>` with total record count.

### Ticket PERF-02: PostgreSQL Indexes on Frequent Search & Foreign Key Columns
- **Problem:** Full-table sequential scans may occur on large datasets when searching by case number, national ID, or hearing date.
- **Solution:** Add explicit EF Core index definitions in `ApplicationDbContext`:
  - `cases (CaseNumber, FilingDate)`
  - `case_hearings (HearingDate, CaseId)`
  - `clients (NationalIdNumber, FullName)`
  - `companies (RegistrationNumber, CompanyName)`

### Ticket PERF-03: Cache Headers & In-Memory Caching for Financial Ledger Summary
- **Problem:** `/api/financials/summary` recalculates the grand firm total by querying all active cases and companies on every request.
- **Solution:** Implement a short sliding-window cache (e.g., `IMemoryCache` for 30–60 seconds, invalidated upon transaction creation/edit) or utilize HTTP `ETag` / `Cache-Control` headers.

### Ticket PERF-04: ASP.NET Core 8 Rate Limiting Middleware
- **Problem:** High-frequency authentication or API polling could be abused without rate limiting.
- **Solution:** Configure `app.UseRateLimiter()` in `LegalERP.Api` with a sliding window rate limiter (e.g. 100 requests per minute per IP for general endpoints, 10 per minute for `/api/auth/login`).

### Ticket PERF-05: Integrate Automated k6 Performance Gates in CI/CD
- **Problem:** New feature additions might introduce silent N+1 queries or performance regressions without automated detection.
- **Solution:** Commit the `.scratch/k6-performance/modules-benchmark.js` script to the repository and add a GitHub Actions / local pre-release step that fails if `p(95)` exceeds 300ms.
