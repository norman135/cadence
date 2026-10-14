// Measures how smoothly a 500-card board drags (the M4 goal is 60 fps).
//
// Serves the production build with `vite preview`, mocks the API with a 500-card board, then drags
// a card across three columns in real time (one pointer move per frame for two seconds) while
// recording every frame's duration. Runs twice: as is, and with the CPU throttled 4x to stand in
// for a slower laptop.
//
//   npm run build && node scripts/measure-board-fps.mjs
// Code inside page.evaluate runs in the browser.
/* global window, requestAnimationFrame */
import { spawn } from 'node:child_process';
import { chromium } from '@playwright/test';

const PORT = 4179;
const CARDS = 500;
const org = {
  id: '0199a1b2-0000-7000-8000-000000000001',
  name: 'Perf',
  slug: 'perf',
  role: 'Owner',
};
const project = {
  id: '0199a1b2-0000-7000-8000-0000000000a1',
  key: 'PRF',
  name: 'Performance',
  description: null,
  role: 'Admin',
  openIssueCount: CARDS,
  createdAt: '2026-10-01T00:00:00Z',
  archivedAt: null,
};
const categories = ['Backlog', 'Todo', 'InProgress', 'InReview', 'Done', 'Canceled'];
const digits = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz';
const rank = (index) => `b${digits[Math.floor(index / 62)]}${digits[index % 62]}`;
const people = ['Ada Lovelace', 'Grace Hopper', 'Alan Turing', 'Katherine Johnson'];

const columns = categories.map((category, position) => ({
  statusId: `status-${category}`,
  name: category,
  category,
  wipLimit: null,
  total: 0,
  hasMore: false,
  nextRank: null,
  cards: [],
  position,
}));
for (let index = 0; index < CARDS; index++) {
  // Most cards in the open columns, as on a busy board.
  const column = columns[[0, 1, 1, 2, 2, 3, 4][index % 7]];
  column.cards.push({
    id: `card-${index}`,
    key: `PRF-${index + 1}`,
    type: 'Task',
    title: `Card ${index + 1}: make the board fast with many cards on it`,
    statusId: column.statusId,
    priority: ['None', 'Low', 'Medium', 'High', 'Urgent'][index % 5],
    rank: rank(index),
    version: 1,
    assignee: index % 3 ? { id: `user-${index % 4}`, displayName: people[index % 4] } : null,
    labels: index % 2 ? [{ id: 'l1', name: 'perf', color: 'Teal' }] : [],
    estimate: index % 4 ? 3 : null,
    dueDate: null,
    subIssueCount: 0,
    subIssueClosedCount: 0,
    commentCount: index % 5,
    epic: null,
  });
}
for (const column of columns) column.total = column.cards.length;

const board = { projectId: project.id, projectKey: project.key, closedWindowDays: 14, columns };
const workflow = {
  id: 'wf',
  projectId: project.id,
  restrictTransitions: false,
  statuses: columns.map((column) => ({
    id: column.statusId,
    name: column.name,
    category: column.category,
    position: column.position,
    wipLimit: null,
    isDefault: column.category === 'Todo',
  })),
  transitions: [],
};

function respond(pathname) {
  if (pathname === '/api/v1/auth/refresh')
    return { accessToken: 'perf', expiresAt: '2099-01-01T00:00:00Z' };
  if (pathname === '/api/v1/me') {
    return {
      id: 'user-0',
      email: 'ada@perf.test',
      displayName: 'Ada Lovelace',
      organizations: [org],
    };
  }
  if (pathname.endsWith('/projects')) return [project];
  if (pathname.endsWith('/workflow')) return workflow;
  if (pathname.endsWith('/board/moves')) {
    return { issueId: 'card-0', statusId: 'status-Done', category: 'Done', rank: 'a0', version: 2 };
  }
  if (pathname.endsWith('/board')) return board;
  if (pathname.endsWith('/labels')) return [];
  if (pathname.endsWith(`/projects/${project.id}/members`)) return [];
  if (pathname.endsWith('/members')) return { items: [], nextCursor: null };
  return null;
}

async function measure(browser, throttle) {
  const page = await browser.newPage({ viewport: { width: 1600, height: 1000 } });
  page.on('console', (message) => {
    if (message.type() === 'error') console.error('console:', message.text());
  });
  page.on('requestfailed', (request) => {
    console.error('requestfailed:', request.url());
  });
  page.on('pageerror', (error) => {
    console.error('pageerror:', error.message);
  });
  await page.route('**/api/v1/**', (route) => {
    const body = respond(new URL(route.request().url()).pathname);
    return body === null
      ? route.fulfill({ status: 404, body: '{}' })
      : route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
  });
  const cdp = await page.context().newCDPSession(page);
  await page.goto(`http://localhost:${PORT}/perf/projects/PRF/board`);
  await page
    .getByRole('list', { name: 'Todo cards' })
    .getByRole('listitem')
    .first()
    .waitFor({ timeout: 15_000 })
    .catch(async (error) => {
      console.error(page.url(), (await page.locator('body').innerText()).slice(0, 500));
      throw error;
    });
  await cdp.send('Emulation.setCPUThrottlingRate', { rate: throttle });
  await cdp.send('Performance.enable');
  const metric = async () =>
    Object.fromEntries(
      (await cdp.send('Performance.getMetrics')).metrics.map((entry) => [entry.name, entry.value]),
    );
  const before = await metric();

  const card = page.getByRole('list', { name: 'Todo cards' }).getByRole('link').first();
  const from = await card.boundingBox();
  const target = await page.getByRole('list', { name: 'Done cards' }).boundingBox();
  if (!from || !target) throw new Error('Board did not render');

  await page.evaluate(() => {
    window.__frames = [];
    let last = performance.now();
    const tick = (now) => {
      window.__frames.push(now - last);
      last = now;
      if (window.__recording) requestAnimationFrame(tick);
    };
    window.__recording = true;
    requestAnimationFrame(tick);
  });

  const start = { x: from.x + from.width / 2, y: from.y + from.height / 2 };
  await page.mouse.move(start.x, start.y);
  await page.mouse.down();
  const steps = 120;
  for (let step = 1; step <= steps; step++) {
    const t = step / steps;
    await page.mouse.move(
      start.x + (target.x + 140 - start.x) * t,
      start.y + 300 * Math.sin(Math.PI * t),
    );
    await page.waitForTimeout(16);
  }
  await page.mouse.up();

  const after = await metric();
  const spent = (name) => Math.round((after[name] - before[name]) * 1000);
  const frames = await page.evaluate(() => {
    window.__recording = false;
    return window.__frames.slice(1);
  });
  await page.close();

  const sorted = [...frames].sort((a, b) => a - b);
  const total = frames.reduce((sum, frame) => sum + frame, 0);
  return {
    throttle: `${throttle}x CPU`,
    frames: frames.length,
    fps: Math.round((frames.length / total) * 1000),
    p95: Number(sorted[Math.floor(sorted.length * 0.95)].toFixed(1)),
    dropped: frames.filter((frame) => frame > (1000 / 60) * 1.5).length,
    scriptMs: spent('ScriptDuration'),
    styleMs: spent('RecalcStyleDuration'),
    layoutMs: spent('LayoutDuration'),
  };
}

const preview = spawn('npx', ['vite', 'preview', '--port', String(PORT), '--strictPort'], {
  shell: true,
  stdio: 'ignore',
});
try {
  for (let attempt = 0; attempt < 50; attempt++) {
    try {
      await fetch(`http://localhost:${PORT}/`);
      break;
    } catch {
      await new Promise((resolve) => setTimeout(resolve, 200));
    }
  }

  const browser = await chromium.launch();
  const results = [await measure(browser, 1), await measure(browser, 4)];
  await browser.close();
  console.log(`Dragging one card across a ${CARDS}-card board:`);
  console.table(results);
} finally {
  preview.kill();
}
