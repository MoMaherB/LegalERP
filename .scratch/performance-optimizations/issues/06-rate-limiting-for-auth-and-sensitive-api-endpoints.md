# 06: Rate Limiting for Auth and Sensitive API Endpoints

**What to build:** Configure ASP.NET Core 8 Rate Limiting middleware to protect the web application against brute-force credential stuffing and runaway API consumers, returning HTTP 429 Too Many Requests when limits are exceeded.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] Rate limiting middleware (`Microsoft.AspNetCore.RateLimiting`) is registered and configured with policies:
  - Strict policy for `/api/auth/login` (e.g. max 10 requests per minute per IP address).
  - General API policy for public/authenticated endpoints (e.g. 150 requests per minute).
- [x] Rate-limited responses return status code `429 Too Many Requests` with a clear localized error message or `Retry-After` header.
- [x] Whitelist or bypass is provided for internal server-to-server calls or loopback traffic.
- [x] Automated tests verify that requests within limits succeed and requests exceeding the threshold return HTTP 429.
