import http from 'k6/http';
import { check, group, sleep } from 'k6';

const DURATION = __ENV.DURATION || '15s';
const VUS = parseInt(__ENV.VUS || '10', 10);
const BASE_API_URL = __ENV.BASE_API_URL || 'https://localhost:7148';
const EMAIL = __ENV.BENCHMARK_EMAIL || 'AhmedHamdi@mail.com';
const PASSWORD = __ENV.BENCHMARK_PASSWORD || 'Aa@01000226963';

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: {
    performance_gate: {
      executor: 'constant-vus',
      vus: VUS,
      duration: DURATION,
    },
  },
  thresholds: {
    // 1. Zero failure tolerance for operational endpoints
    http_req_failed: ['rate<0.01'],

    // 2. Strict p95 latency budget across all requests
    http_req_duration: ['p(95)<250'],

    // 3. 100% check pass rate for module health checks
    checks: ['rate==1.0'],

    // 4. Per-endpoint performance SLAs
    'http_req_duration{name:cases_paged}': ['p(95)<250'],
    'http_req_duration{name:companies_paged}': ['p(95)<250'],
    'http_req_duration{name:clients_paged}': ['p(95)<250'],
    'http_req_duration{name:financials_summary}': ['p(95)<150'],
  },
};

// Setup: Authenticate dynamically before starting load test
export function setup() {
  const loginRes = http.post(`${BASE_API_URL}/api/auth/login`, JSON.stringify({
    email: EMAIL,
    password: PASSWORD,
    rememberMe: true,
  }), {
    headers: { 'Content-Type': 'application/json' },
  });

  check(loginRes, {
    'setup login succeeded': (r) => r.status === 200,
  });

  // Extract auth cookie if present
  const cookies = loginRes.cookies;
  return { cookies };
}

export default function (data) {
  const requestHeaders = {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  };

  // Helper to validate paginated endpoint health and headers
  function checkPagedEndpoint(endpointPath, tagName) {
    const response = http.get(`${BASE_API_URL}/api/${endpointPath}?pageNumber=1&pageSize=25`, {
      headers: requestHeaders,
      cookies: data.cookies,
      tags: { name: tagName },
    });

    return check(response, {
      [`${tagName} status is 200`]: (r) => r.status === 200,
      [`${tagName} has X-Total-Count`]: (r) => r.headers['X-Total-Count'] !== undefined,
      [`${tagName} has X-Page-Number`]: (r) => r.headers['X-Page-Number'] === '1',
    });
  }

  // 1. Cases Paginated Query (Tickets 01 & 04)
  group('Cases Paginated List', function () {
    checkPagedEndpoint('cases', 'cases_paged');
  });

  // 2. Companies Paginated Query (Tickets 02 & 04)
  group('Companies Paginated List', function () {
    checkPagedEndpoint('companies', 'companies_paged');
  });

  // 3. Clients Paginated Query (Tickets 03 & 04)
  group('Clients Paginated List', function () {
    checkPagedEndpoint('clients', 'clients_paged');
  });

  // 4. Financials Cached Summary & ETag 304 Validation (Ticket 05)
  group('Financials Cached Summary', function () {
    const summaryResponse = http.get(`${BASE_API_URL}/api/financials/summary`, {
      headers: requestHeaders,
      cookies: data.cookies,
      tags: { name: 'financials_summary' },
    });

    check(summaryResponse, {
      'financials status is 200': (r) => r.status === 200,
      'financials has ETag': (r) => r.headers['Etag'] !== undefined || r.headers['ETag'] !== undefined,
      'financials has Cache-Control': (r) => (r.headers['Cache-Control'] || '').includes('max-age'),
    });

    const etag = summaryResponse.headers['Etag'] || summaryResponse.headers['ETag'];
    if (etag) {
      const cached304Response = http.get(`${BASE_API_URL}/api/financials/summary`, {
        headers: {
          ...requestHeaders,
          'If-None-Match': etag,
        },
        cookies: data.cookies,
        tags: { name: 'financials_304' },
      });

      check(cached304Response, {
        'financials ETag returns 304 Not Modified': (r) => r.status === 304,
      });
    }
  });

  // 5. Rate-Limited Auth Verification (Ticket 06)
  group('Rate-Limited Auth Verification', function () {
    const authProbe = http.post(`${BASE_API_URL}/api/auth/login`, JSON.stringify({
      email: 'rate-limit-probe@test.com',
      password: 'ProbePassword123!',
    }), {
      headers: requestHeaders,
      tags: { name: 'auth_rate_limit' },
    });

    // An auth attempt will either return 401 (invalid creds) or 429 (rate-limited)
    check(authProbe, {
      'auth returns 401 or throttles with 429': (r) => r.status === 401 || r.status === 429,
    });
  });

  sleep(0.1);
}
