// Enforces the frontend performance budgets from docs/ROADMAP.md §3.
//
// Reads Vite's build manifest to tell the *initial* download (the entry chunk plus everything it
// imports statically) apart from lazily loaded route chunks, then measures gzip sizes, which is
// what users actually download. Exits with code 1 when a budget is exceeded.
//
// Usage: npm run build && npm run budget

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { gzipSync } from 'node:zlib';

const KB = 1024;
const BUDGETS = {
  initialJs: 180 * KB,
  initialCss: 30 * KB,
  lazyChunk: 80 * KB,
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
const lazyChunks = Object.entries(manifest)
  .filter(([key, chunk]) => chunk.isDynamicEntry && !initialChunks.has(key))
  .map(([key, chunk]) => ({ name: chunk.name ?? key, file: chunk.file }));

const sum = (files) => files.reduce((total, file) => total + gzipSize(file), 0);

const results = [
  { check: 'Initial JavaScript', size: sum(initialJsFiles), budget: BUDGETS.initialJs },
  { check: 'Initial CSS', size: sum(initialCssFiles), budget: BUDGETS.initialCss },
  ...lazyChunks.map(({ name, file }) => ({
    check: `Lazy chunk: ${name}`,
    size: gzipSize(file),
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
