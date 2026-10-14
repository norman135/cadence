// The Kanban board under load: the M4 gate is a card move persisting with p95 <= 50 ms.
//
// Needs the seeded demo organization (Cadence.Migrator seed), for example:
//   docker compose run --rm migrate seed --issues 10000 --password <password>
//   k6 run -e SEED_PASSWORD=<password> perf/k6/board.js
//
// Each iteration is one person at the board: they load it (sometimes with a quick filter), then
// move a card to another column or reorder it within its column, as a drag would.

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
    'http_req_duration{kind:move}': [`p(95)<${VUS > 10 ? 150 : 50}`],
    'http_req_duration{kind:board}': [`p(95)<${VUS > 10 ? 500 : 200}`],
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
  const headers = { Authorization: `Bearer ${login.json('accessToken')}`, 'Content-Type': 'application/json' };

  const me = http.get(`${BASE_URL}/api/v1/me`, { headers }).json();
  const organization = me.organizations.find((org) => org.slug === 'lumen-labs');
  if (!organization) fail('The lumen-labs organization is missing; run the seeder first.');

  const base = `${BASE_URL}/api/v1/organizations/${organization.id}`;
  const projects = http.get(`${base}/projects`, { headers }).json();

  // Access tokens last 10 minutes, which covers a run of a few minutes.
  return { headers, base, me: me.id, projects };
}

const pick = (items) => items[Math.floor(Math.random() * items.length)];

// Two people can grab the same card; the loser gets 409 and would reload. That is the conflict
// handling working, not a failure.
const moveResponses = http.expectedStatuses(200, 409);

export default function (data) {
  const { headers, base, projects } = data;
  const project = pick(projects);
  const filters = ['', '', '', `?assigneeId=${data.me}`, '?type=Bug', '?priority=Urgent&priority=High'];

  const response = http.get(`${base}/projects/${project.id}/board${pick(filters)}`, {
    headers,
    tags: { kind: 'board' },
  });
  check(response, { 'board loads': (r) => r.status === 200 });
  const board = response.json();

  const open = board.columns.filter((column) => column.category !== 'Done' && column.category !== 'Canceled');
  const from = pick(open.filter((column) => column.cards.length > 1).concat(open.filter((c) => c.cards.length > 0)));
  if (!from) return;

  const index = Math.floor(Math.random() * from.cards.length);
  const card = from.cards[index];
  let move;
  if (Math.random() < 0.5 && index > 0) {
    // Reorder: drop the card right after the first card of its column. Appending a digit to a key
    // gives a key just after it.
    move = { toStatusId: from.statusId, rank: `${from.cards[0].rank}V` };
  } else {
    // Another column, keeping its place in the project's order.
    move = { toStatusId: pick(open.filter((column) => column.statusId !== from.statusId)).statusId, rank: card.rank };
  }

  const moved = http.post(
    `${base}/projects/${project.id}/board/moves`,
    JSON.stringify({
      issueId: card.id,
      fromStatusId: card.statusId,
      toStatusId: move.toStatusId,
      rank: move.rank,
      version: card.version,
      assigneeId: card.assignee ? card.assignee.id : null,
    }),
    { headers, tags: { kind: 'move' }, responseCallback: moveResponses },
  );
  check(moved, { 'move persists or reports a conflict': (r) => r.status === 200 || r.status === 409 });

  sleep(1);
}
