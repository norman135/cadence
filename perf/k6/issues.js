// Issue list and detail under load: the M3 gate is p95 <= 100 ms with 10,000 issues.
//
// Needs the seeded demo organization (Cadence.Migrator seed), for example:
//   docker compose run --rm migrate seed --issues 10000 --password <password>
//   k6 run -e SEED_PASSWORD=<password> perf/k6/issues.js
//
// Each iteration is one person working: they open a filtered or sorted list, sometimes the next
// page, and then an issue from it.

import { check, fail, sleep } from 'k6';
import http from 'k6/http';

const BASE_URL = __ENV.BASE_URL || 'https://localhost';
const VUS = Number(__ENV.VUS || 10);
const EMAIL = __ENV.SEED_EMAIL || 'ada@lumenlabs.test';

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: {
    team: { executor: 'constant-vus', vus: VUS, duration: __ENV.DURATION || '1m' },
  },
  thresholds: {
    http_req_failed: ['rate==0'],
    checks: ['rate==1'],
    'http_req_duration{kind:list}': [`p(95)<${VUS > 10 ? 300 : 100}`],
    'http_req_duration{kind:detail}': [`p(95)<${VUS > 10 ? 300 : 100}`],
  },
  summaryTrendStats: ['avg', 'med', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  if (!__ENV.SEED_PASSWORD) fail('Set SEED_PASSWORD to the password the seeder used.');

  const login = http.post(
    `${BASE_URL}/api/v1/auth/login`,
    JSON.stringify({ email: EMAIL, password: __ENV.SEED_PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } },
  );
  if (login.status !== 200) fail(`Sign-in failed: ${login.status}`);
  const headers = { Authorization: `Bearer ${login.json('accessToken')}` };

  const me = http.get(`${BASE_URL}/api/v1/me`, { headers }).json();
  const organization = me.organizations.find((org) => org.slug === 'lumen-labs');
  if (!organization) fail('The lumen-labs organization is missing; run the seeder first.');

  const base = `${BASE_URL}/api/v1/organizations/${organization.id}`;
  const projects = http.get(`${base}/projects`, { headers }).json();
  const labels = http.get(`${base}/labels`, { headers }).json();

  // Access tokens last 10 minutes, which covers a run of a few minutes.
  return { headers, base, me: me.id, projects, labels };
}

const pick = (items) => items[Math.floor(Math.random() * items.length)];

export default function (data) {
  const { headers, base, projects, labels } = data;
  const project = pick(projects);

  const lists = [
    '',
    '?status=Todo&status=InProgress&status=InReview',
    '?sort=Priority',
    '?sort=DueDate',
    `?assigneeId=${data.me}&status=Todo&status=InProgress`,
    `?projectId=${project.id}&labelId=${pick(labels).id}`,
    `?projectId=${project.id}&sort=Created`,
    `?q=${encodeURIComponent(pick(['sync', 'android', 'search', 'webhook']))}`,
    `?priority=Urgent&priority=High&projectId=${project.id}`,
  ];

  const query = pick(lists);
  const list = http.get(`${base}/issues${query}`, { headers, tags: { kind: 'list' } });
  check(list, { 'list loads': (response) => response.status === 200 });
  const page = list.json();

  if (page.nextCursor && Math.random() < 0.3) {
    // A cursor belongs to its query: the next page keeps the same filters and sort.
    const separator = query ? '&' : '?';
    const next = http.get(`${base}/issues${query}${separator}after=${page.nextCursor}`, {
      headers,
      tags: { kind: 'list' },
    });
    check(next, { 'next page loads': (response) => response.status === 200 });
  }

  if (page.items.length > 0) {
    const issue = pick(page.items);
    const detail = http.get(`${base}/issues/by-key/${issue.key}`, { headers, tags: { kind: 'detail' } });
    check(detail, { 'issue loads': (response) => response.status === 200 });
  }

  sleep(1);
}
