// Regenerates the visual regression baselines (web/visual/__screenshots__) from any OS.
//
// Screenshots depend on the operating system's font rendering, so baselines are made in the
// same Playwright Linux image the CI "Visual" job runs in. Needs Docker.
//
//   npm run test:visual:update
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const web = join(dirname(fileURLToPath(import.meta.url)), '..');
const repo = join(web, '..');
const { version } = JSON.parse(
  readFileSync(join(web, 'node_modules', '@playwright', 'test', 'package.json'), 'utf8'),
);
const image = `mcr.microsoft.com/playwright:v${version}-noble`;

console.log(`Updating visual baselines in ${image}…`);
const result = spawnSync(
  'docker',
  [
    'run',
    '--rm',
    '--ipc=host',
    '-v',
    `${repo}:/work`,
    // Linux needs its own node_modules (native binaries), kept in a volume between runs.
    '-v',
    'cadence-visual-node-modules:/work/web/node_modules',
    '-w',
    '/work/web',
    image,
    'bash',
    '-c',
    'npm ci --no-audit --no-fund && npx playwright test -c playwright.visual.config.ts --update-snapshots',
  ],
  { stdio: 'inherit' },
);
process.exit(result.status ?? 1);
