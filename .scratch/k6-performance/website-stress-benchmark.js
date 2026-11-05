import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Trend, Rate, Counter } from 'k6/metrics';

const journeyLatency = new Trend('user_journey_latency');
const writeLatency = new Trend('write_operations_latency');
const stressErrors = new Rate('stress_errors');
const completedJourneys = new Counter('completed_journeys');

export const options = {
  insecureSkipTLSVerify: true,
  stages: [
    { duration: '10s', target: 15 }, // ramp up to 15 concurrent users
    { duration: '20s', target: 35 }, // ramp up to 35 concurrent users
    { duration: '15s', target: 50 }, // spike to 50 concurrent users
    { duration: '10s', target: 0 },  // ramp down to 0
  ],
  thresholds: {
    stress_errors: ['rate<0.02'],       // less than 2% errors under heavy load
    http_req_duration: ['p(95)<400'],   // 95% of all requests under 400ms
    user_journey_latency: ['p(90)<500'],
  },
};

const BASE_API_URL = 'https://localhost:7148';
const BASE_WEB_URL = 'https://localhost:7209';
const TOKEN = 'eyJJZCI6ImYwNjk4M2JjLTFiYTgtNDc3My04ZTVhLTNmMzhkZjcxOTQyNiIsIkZ1bGxOYW1lIjoiXHUwNjIzXHUwNjJEXHUwNjQ1XHUwNjJGIFx1MDYyRFx1MDY0NVx1MDYyRlx1MDY0QSIsIkVtYWlsIjoiQWhtZWRIYW1kaUBtYWlsLmNvbSIsIlJvbGUiOiJTdXBlckFkbWluIiwiRXhwIjoxNzk2NDY0MzI0fQ==.Z3uTowR/+Pn0XwYPhbIwd/gTolR2XdcWnd3SGC1Swv0=';

const authHeaders = {
  headers: {
    'Authorization': `Bearer ${TOKEN}`,
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  },
};

export default function () {
  const vuId = __VU;
  const iteration = __ITER;
  const journeyType = (vuId + iteration) % 5;

  const t0 = new Date();

  if (journeyType === 0) {
    // ── Journey A: Case Manager Workflow ─────────────────────────────────
    group('Journey: Case Manager', function () {
      const list = http.get(`${BASE_API_URL}/api/cases`, authHeaders);
      const okList = check(list, { 'cases list ok': (r) => r.status === 200 });
      stressErrors.add(!okList);

      const search = http.get(`${BASE_API_URL}/api/cases/search?term=قضية`, authHeaders);
      const okSearch = check(search, { 'cases search ok': (r) => r.status === 200 });
      stressErrors.add(!okSearch);

      const page = http.get(`${BASE_WEB_URL}/cases`);
      const okPage = check(page, { 'cases web page ok': (r) => r.status === 200 || r.status === 302 });
      stressErrors.add(!okPage);
    });
  } else if (journeyType === 1) {
    // ── Journey B: Corporate Specialist Workflow ─────────────────────────
    group('Journey: Corporate Specialist', function () {
      const list = http.get(`${BASE_API_URL}/api/companies`, authHeaders);
      const okList = check(list, { 'companies list ok': (r) => r.status === 200 });
      stressErrors.add(!okList);

      const search = http.get(`${BASE_API_URL}/api/companies/search?term=شركة&category=CapitalCompany`, authHeaders);
      const okSearch = check(search, { 'companies search ok': (r) => r.status === 200 });
      stressErrors.add(!okSearch);

      const page = http.get(`${BASE_WEB_URL}/companies`);
      const okPage = check(page, { 'companies web page ok': (r) => r.status === 200 || r.status === 302 });
      stressErrors.add(!okPage);
    });
  } else if (journeyType === 2) {
    // ── Journey C: Client Intake & 360 Profile ───────────────────────────
    group('Journey: Client 360', function () {
      const list = http.get(`${BASE_API_URL}/api/clients`, authHeaders);
      const okList = check(list, { 'clients list ok': (r) => r.status === 200 });
      stressErrors.add(!okList);

      const search = http.get(`${BASE_API_URL}/api/clients/search?term=علي`, authHeaders);
      const okSearch = check(search, { 'clients search ok': (r) => r.status === 200 });
      stressErrors.add(!okSearch);

      const page = http.get(`${BASE_WEB_URL}/clients`);
      const okPage = check(page, { 'clients web page ok': (r) => r.status === 200 || r.status === 302 });
      stressErrors.add(!okPage);
    });
  } else if (journeyType === 3) {
    // ── Journey D: Financials & Ledger Auditing ───────────────────────────
    group('Journey: Financial Auditor', function () {
      const summary = http.get(`${BASE_API_URL}/api/financials/summary`, authHeaders);
      const okSummary = check(summary, { 'financials summary ok': (r) => r.status === 200 });
      stressErrors.add(!okSummary);

      const page = http.get(`${BASE_WEB_URL}/financials`);
      const okPage = check(page, { 'financials web page ok': (r) => r.status === 200 || r.status === 302 });
      stressErrors.add(!okPage);
    });
  } else {
    // ── Journey E: Notifications & Attorneys ─────────────────────────────
    group('Journey: Staff Notifications & Attorneys', function () {
      const notifs = http.get(`${BASE_API_URL}/api/notifications`, authHeaders);
      const okNotifs = check(notifs, { 'notifications list ok': (r) => r.status === 200 });
      stressErrors.add(!okNotifs);

      const unread = http.get(`${BASE_API_URL}/api/notifications/unread-count`, authHeaders);
      const okUnread = check(unread, { 'unread count ok': (r) => r.status === 200 });
      stressErrors.add(!okUnread);

      const attorneys = http.get(`${BASE_API_URL}/api/attorneys`, authHeaders);
      const okAttorneys = check(attorneys, { 'attorneys list ok': (r) => r.status === 200 });
      stressErrors.add(!okAttorneys);
    });
  }

  // ── Write traffic sample: create a case periodically (1 in every 10 iterations)
  if (iteration % 10 === 0 && vuId === 1) {
    group('Write Traffic: Create & Delete Case', function () {
      const writeT0 = new Date();
      const newCasePayload = JSON.stringify({
        caseNumber: `PERF-TEST-${Date.now()}`,
        title: 'قضية فحص الأداء التلقائي',
        caseType: 'Civil',
        status: 'Active',
        outcome: null,
        filingDate: null,
        courtName: 'محكمة استئناف القاهرة',
        judgeName: 'دائرة الاستئناف',
        notes: 'تم إنشاؤها عبر اختبار الأداء التلقائي'
      });

      const createRes = http.post(`${BASE_API_URL}/api/cases`, newCasePayload, authHeaders);
      writeLatency.add(new Date() - writeT0);

      const okCreate = check(createRes, { 'case created successfully': (r) => r.status === 200 || r.status === 201 });
      stressErrors.add(!okCreate);

      if (createRes.status === 200 || createRes.status === 201) {
        try {
          const createdCase = JSON.parse(createRes.body);
          if (createdCase && createdCase.id) {
            http.del(`${BASE_API_URL}/api/cases/${createdCase.id}`, null, authHeaders);
          }
        } catch (e) {
          // ignore parsing error
        }
      }
    });
  }

  journeyLatency.add(new Date() - t0);
  completedJourneys.add(1);

  sleep(0.2);
}
