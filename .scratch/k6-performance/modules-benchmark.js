import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';

// Module latencies
const authLatency = new Trend('auth_latency');
const casesLatency = new Trend('cases_latency');
const companiesLatency = new Trend('companies_latency');
const clientsLatency = new Trend('clients_latency');
const attorneysLatency = new Trend('attorneys_latency');
const financialsLatency = new Trend('financials_latency');
const notificationsLatency = new Trend('notifications_latency');
const usersLatency = new Trend('users_latency');
const webUiLatency = new Trend('web_ui_latency');
const errorRate = new Rate('custom_error_rate');

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: {
    module_benchmark: {
      executor: 'constant-vus',
      vus: 10,
      duration: '20s',
    },
  },
  thresholds: {
    custom_error_rate: ['rate<0.01'], // less than 1% errors
    http_req_duration: ['p(95)<300'],
    cases_latency: ['p(95)<200'],
    companies_latency: ['p(95)<200'],
    clients_latency: ['p(95)<200'],
    attorneys_latency: ['p(95)<200'],
    financials_latency: ['p(95)<200'],
    notifications_latency: ['p(95)<200'],
    users_latency: ['p(95)<200'],
    web_ui_latency: ['p(95)<300'],
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
  // 1. Auth Module
  group('Module: Auth', function () {
    const t0 = new Date();
    const meRes = http.get(`${BASE_API_URL}/api/auth/me`, authHeaders);
    authLatency.add(new Date() - t0);

    const ok = check(meRes, {
      'auth/me status is 200': (r) => r.status === 200,
      'auth/me has valid email': (r) => r.body.includes('AhmedHamdi@mail.com'),
    });
    errorRate.add(!ok);
  });

  // 2. Cases Module
  group('Module: Cases', function () {
    const t0 = new Date();
    const listRes = http.get(`${BASE_API_URL}/api/cases`, authHeaders);
    casesLatency.add(new Date() - t0);

    const okList = check(listRes, {
      'cases list status is 200': (r) => r.status === 200,
    });
    errorRate.add(!okList);

    const t1 = new Date();
    const searchRes = http.get(`${BASE_API_URL}/api/cases/search?term=دعوى`, authHeaders);
    casesLatency.add(new Date() - t1);

    const okSearch = check(searchRes, {
      'cases search status is 200': (r) => r.status === 200,
    });
    errorRate.add(!okSearch);
  });

  // 3. Companies Module
  group('Module: Companies', function () {
    const t0 = new Date();
    const listRes = http.get(`${BASE_API_URL}/api/companies`, authHeaders);
    companiesLatency.add(new Date() - t0);

    const okList = check(listRes, {
      'companies list status is 200': (r) => r.status === 200,
    });
    errorRate.add(!okList);

    const t1 = new Date();
    const searchRes = http.get(`${BASE_API_URL}/api/companies/search?term=شركة&category=CapitalCompany`, authHeaders);
    companiesLatency.add(new Date() - t1);

    const okSearch = check(searchRes, {
      'companies search status is 200': (r) => r.status === 200,
    });
    errorRate.add(!okSearch);
  });

  // 4. Clients Module
  group('Module: Clients', function () {
    const t0 = new Date();
    const listRes = http.get(`${BASE_API_URL}/api/clients`, authHeaders);
    clientsLatency.add(new Date() - t0);

    const okList = check(listRes, {
      'clients list status is 200': (r) => r.status === 200,
    });
    errorRate.add(!okList);

    const t1 = new Date();
    const searchRes = http.get(`${BASE_API_URL}/api/clients/search?term=أحمد`, authHeaders);
    clientsLatency.add(new Date() - t1);

    const okSearch = check(searchRes, {
      'clients search status is 200': (r) => r.status === 200,
    });
    errorRate.add(!okSearch);
  });

  // 5. Attorneys Module
  group('Module: Attorneys', function () {
    const t0 = new Date();
    const listRes = http.get(`${BASE_API_URL}/api/attorneys`, authHeaders);
    attorneysLatency.add(new Date() - t0);

    const okList = check(listRes, {
      'attorneys list status is 200': (r) => r.status === 200,
    });
    errorRate.add(!okList);
  });

  // 6. Financials Module
  group('Module: Financials', function () {
    const t0 = new Date();
    const finRes = http.get(`${BASE_API_URL}/api/financials/summary`, authHeaders);
    financialsLatency.add(new Date() - t0);

    const ok = check(finRes, {
      'financials summary status is 200': (r) => r.status === 200,
      'financials summary has totals': (r) => r.body.includes('firmTotalAgreedFees'),
    });
    errorRate.add(!ok);
  });

  // 7. Notifications Module
  group('Module: Notifications', function () {
    const t0 = new Date();
    const notifRes = http.get(`${BASE_API_URL}/api/notifications`, authHeaders);
    notificationsLatency.add(new Date() - t0);

    const okList = check(notifRes, {
      'notifications list status is 200': (r) => r.status === 200,
    });
    errorRate.add(!okList);

    const t1 = new Date();
    const unreadRes = http.get(`${BASE_API_URL}/api/notifications/unread-count`, authHeaders);
    notificationsLatency.add(new Date() - t1);

    const okCount = check(unreadRes, {
      'notifications unread count status is 200': (r) => r.status === 200,
    });
    errorRate.add(!okCount);
  });

  // 8. Users & Administration Module
  group('Module: Users & Administration', function () {
    const t0 = new Date();
    const usersRes = http.get(`${BASE_API_URL}/api/users`, authHeaders);
    usersLatency.add(new Date() - t0);

    const okUsers = check(usersRes, {
      'users list status is 200': (r) => r.status === 200,
    });
    errorRate.add(!okUsers);
  });

  // 9. Web UI Frontend Server-Rendered Views
  group('Module: Web UI Frontend', function () {
    const t0 = new Date();
    const homeRes = http.get(`${BASE_WEB_URL}/`);
    webUiLatency.add(new Date() - t0);

    const okHome = check(homeRes, {
      'home page status is 200 or 302': (r) => r.status === 200 || r.status === 302,
    });
    errorRate.add(!okHome);

    const t1 = new Date();
    const casesPageRes = http.get(`${BASE_WEB_URL}/cases`);
    webUiLatency.add(new Date() - t1);

    const okCases = check(casesPageRes, {
      'cases page status is 200 or 302': (r) => r.status === 200 || r.status === 302,
    });
    errorRate.add(!okCases);

    const t2 = new Date();
    const companiesPageRes = http.get(`${BASE_WEB_URL}/companies`);
    webUiLatency.add(new Date() - t2);

    const okCompanies = check(companiesPageRes, {
      'companies page status is 200 or 302': (r) => r.status === 200 || r.status === 302,
    });
    errorRate.add(!okCompanies);
  });

  sleep(0.2);
}
