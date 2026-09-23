// Baseline load test: a team of 10 people using Cadence at the same time (the design target),
// with thresholds taken from docs/ROADMAP.md §3. Later milestones extend this with real journeys.
//
//   k6 run perf/k6/baseline.js                                   # https://localhost, 10 users, 1 min
//   k6 run -e VUS=50 -e DURATION=2m perf/k6/baseline.js          # 5x headroom
//   k6 run -e BASE_URL=http://app:8080 perf/k6/baseline.js       # bypass the proxy
//
// Hashed assets are cached immutably by browsers, so a returning user only fetches index.html
// and API data; that is what each iteration models.

import { check, sleep } from 'k6';
import http from 'k6/http';

const BASE_URL = __ENV.BASE_URL || 'https://localhost';
const VUS = Number(__ENV.VUS || 10);

export const options = {
  // Local and CI stacks use Caddy's self-signed certificate for "localhost".
  insecureSkipTLSVerify: true,
  scenarios: {
    team: {
      executor: 'constant-vus',
      vus: VUS,
      duration: __ENV.DURATION || '1m',
    },
  },
  thresholds: {
    http_req_failed: ['rate==0'],
    checks: ['rate==1'],
    // Reads at the design load must stay under 100 ms at p95; the 50-user headroom run is
    // allowed 300 ms (ROADMAP §3).
    'http_req_duration{kind:api}': [`p(95)<${VUS > 10 ? 300 : 100}`],
    'http_req_duration{kind:page}': [`p(95)<${VUS > 10 ? 300 : 100}`],
  },
  summaryTrendStats: ['avg', 'med', 'p(95)', 'p(99)', 'max'],
};

export default function () {
  const page = http.get(`${BASE_URL}/`, { tags: { kind: 'page' } });
  check(page, { 'app shell loads': (response) => response.status === 200 });

  const info = http.get(`${BASE_URL}/api/v1/system/info`, { tags: { kind: 'api' } });
  check(info, { 'API responds': (response) => response.status === 200 });

  // Think time between interactions, as a real user pauses between actions.
  sleep(1);
}
