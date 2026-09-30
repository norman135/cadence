// Renders every design board in design/boards to a PNG in design/exports.
//
//   cd web && npx playwright install chromium   # once
//   node ../design/render.mjs                   # all boards
//   node ../design/render.mjs 05-board           # boards whose name contains "05-board"
//
// Uses the Playwright copy installed for the web app's end-to-end tests, so the design
// folder needs no dependencies of its own. Fonts and icons load from Google Fonts and jsDelivr.
import { mkdirSync, readdirSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const require = createRequire(join(here, '../web/package.json'));
const { chromium } = require('@playwright/test');

const boardsDir = join(here, 'boards');
const exportsDir = join(here, 'exports');
mkdirSync(exportsDir, { recursive: true });

const filter = process.argv[2] ?? '';
const boards = readdirSync(boardsDir)
  .filter((file) => /^\d\d-.+\.html$/.test(file) && file.includes(filter))
  .sort();

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1600, height: 1000 }, deviceScaleFactor: 2 });

for (const board of boards) {
  await page.goto(pathToFileURL(join(boardsDir, board)).href, { waitUntil: 'networkidle' });
  await page.waitForFunction(() => window.boardReady === true);
  const out = join(exportsDir, board.replace(/\.html$/, '.png'));
  await page.screenshot({ path: out, fullPage: true });
  console.log(`rendered ${board}`);
}

await browser.close();
