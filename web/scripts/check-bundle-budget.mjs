// Enforces the frontend performance budgets from docs/ROADMAP.md §3.
//
// Reads Vite's build manifest to tell the *initial* download (the entry chunk plus everything it
// imports statically) apart from lazily loaded route chunks, then measures gzip sizes, which is
// what users actually download. Exits with code 1 when a budget is exceeded.
//
// Usage: npm run build && npm run budget

import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { gzipSync } from 'node:zlib';

const KB = 1024;
const BUDGETS = {
  initialJs: 180 * KB,
  initialCss: 30 * KB,
  lazyChunk: 80 * KB,
  fonts: 60 * KB,
};

const distDir = new URL('../dist/', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');
const manifest = JSON.parse(readFileSync(join(distDir, '.vite', 'manifest.json'), 'utf8'));

const gzipSize = (file) => gzipSync(readFileSync(join(distDir, file)), { level: 9 }).length;
const formatKb = (bytes) => `${(bytes / KB).toFixed(1)} KB`;

// Walk static imports from the entry: these files block the first render.
const initialChunks = new Set();
const visit = (key) => {
  if (initialChunks.has(key)) return;
  initialChunks.add(key);
  for (const imported of manifest[key].imports ?? []) visit(imported);
};
for (const [key, chunk] of Object.entries(manifest)) {
  if (chunk.isEntry) visit(key);
}

const initialJsFiles = [...initialChunks].map((key) => manifest[key].file);
const initialCssFiles = [...new Set([...initialChunks].flatMap((key) => manifest[key].css ?? []))];

// What navigating to a lazy route downloads: its chunk plus every chunk it imports statically
// (shared vendor chunks such as form libraries) that the initial bundle doesn't already contain.
const routeCost = (key) => {
  const files = new Set();
  const walk = (current) => {
    if (initialChunks.has(current) || files.has(manifest[current].file)) return;
    files.add(manifest[current].file);
    for (const imported of manifest[current].imports ?? []) walk(imported);
  };
  walk(key);
  return [...files];
};

const lazyChunks = Object.entries(manifest)
  .filter(([key, chunk]) => chunk.isDynamicEntry && !initialChunks.has(key))
  .map(([key, chunk]) => ({ name: chunk.name ?? key, files: routeCost(key) }));

const sum = (files) => files.reduce((total, file) => total + gzipSize(file), 0);

// Fonts: the Latin files an English-speaking user downloads (other subsets load only when a page
// contains their characters). WOFF2 is already compressed, so this is the file size.
const latinFonts = readdirSync(join(distDir, 'assets')).filter((file) =>
  /-latin-wght-normal-[\w-]+\.woff2$/.test(file),
);
const fontBytes = latinFonts.reduce(
  (total, file) => total + statSync(join(distDir, 'assets', file)).size,
  0,
);

const results = [
  { check: 'Initial JavaScript', size: sum(initialJsFiles), budget: BUDGETS.initialJs },
  { check: 'Initial CSS', size: sum(initialCssFiles), budget: BUDGETS.initialCss },
  { check: `Fonts (Latin, ${latinFonts.length} files)`, size: fontBytes, budget: BUDGETS.fonts },
  ...lazyChunks.map(({ name, files }) => ({
    check: `Lazy chunk: ${name}`,
    size: sum(files),
    budget: BUDGETS.lazyChunk,
  })),
];

console.log('\nBundle budgets (gzip)\n');
for (const { check, size, budget } of results) {
  const status = size <= budget ? 'ok  ' : 'FAIL';
  const usage = `${Math.round((size / budget) * 100)}%`;
  console.log(
    `  ${status}  ${check.padEnd(36)} ${formatKb(size).padStart(9)} / ${formatKb(budget)}  (${usage})`,
  );
}

const failures = results.filter(({ size, budget }) => size > budget);
if (failures.length > 0) {
  console.error(`\n${failures.length} budget(s) exceeded.`);
  process.exit(1);
}
console.log('\nAll bundle budgets met.');
