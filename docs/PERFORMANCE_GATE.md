# Automated Performance Regression Gate

This document details the automated performance regression testing gate integrated into LegalERP.

## Purpose

The performance gate prevents slow code, unindexed queries, non-paginated endpoints, and uncached aggregations from being merged into main or deployed to production. It guarantees that our key API endpoints respond within strict latency and reliability budgets under concurrent load.

## Strict Performance Budgets (Thresholds)

Every execution of the performance gate enforces the following strict Service Level Objectives (SLOs):

| Metric | Target / Threshold | Purpose |
| :--- | :--- | :--- |
| **Overall p95 Response Time** | `< 250 ms` | 95% of all HTTP requests must complete in under a quarter-second. |
| **Cases Paginated Query p95** | `< 200 ms` | Ensures index-backed server-side pagination on `cases` table. |
| **Companies Paginated Query p95**| `< 200 ms` | Ensures index-backed server-side pagination on `companies` table. |
| **Clients Paginated Query p95** | `< 200 ms` | Ensures index-backed server-side pagination on `clients` table. |
| **Financials Cached Summary p95**| `< 150 ms` | Validates in-memory caching for aggregated firm ledger totals. |
| **Financials 304 Not Modified p95**| `< 50 ms` | Validates fast ETag 304 validation without database hits. |
| **Error Rate (`http_req_failed`)**| `< 1.0%` | Zero or near-zero network/HTTP failure under concurrent traffic. |
| **Check Pass Rate** | `> 99.0%` | Asserts pagination headers (`X-Total-Count`, `X-Page-Number`) and ETag headers. |

## How to Run Locally

### Prerequisites

1. **k6 CLI**: Ensure `k6` is installed. On Windows:
   ```powershell
   winget install GrafanaLabs.k6
   ```
2. **API Running**: Start the API server in Release or Debug mode:
   ```powershell
   dotnet run --project LegalERP.Api
   ```

### Executing the Gate

Run the root runner script from PowerShell:

```powershell
# Default run against https://localhost:7148 with 10 VUs for 15 seconds:
.\run-performance-gate.ps1

# Custom duration and target host:
.\run-performance-gate.ps1 -BaseUrl "https://localhost:7148" -VUs 20 -Duration "30s"
```

### Exit Codes

* **`0` (Success)**: All latency p95 thresholds and error rate budgets passed.
* **`1` (Failure)**: One or more thresholds were violated. In CI/CD pipelines, this automatically fails the build.

## Continuous Integration (CI/CD)

To run the gate in GitHub Actions or any CI runner:

```yaml
- name: Run Performance Regression Gate
  shell: pwsh
  run: |
    ./run-performance-gate.ps1 -BaseUrl "https://localhost:7148"
```
