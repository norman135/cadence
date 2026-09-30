// Exports the app icons in web/public from the logo mark.
//
//   cd web && node ../design/export-icons.mjs
//
// favicon.svg is the mark itself. The PNGs are for home screens and install prompts:
// "any" icons keep the rounded tile; full-bleed icons (Apple touch, maskable) fill the square
// and keep the bars inside the 80% safe zone, because the platform applies its own mask.
import { copyFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const require = createRequire(join(here, '../web/package.json'));
const { chromium } = require('@playwright/test');
const publicDir = join(here, '../web/public');

const bars = (scale, offset) => `
  <g transform="translate(${offset} ${offset}) scale(${scale})">
    <rect x="15" y="33" width="8" height="16" rx="4" fill="#ffffff" fill-opacity="0.72"/>
    <rect x="28" y="23" width="8" height="26" rx="4" fill="#ffffff"/>
    <rect x="41" y="15" width="8" height="34" rx="4" fill="#ff7b45"/>
  </g>`;

const gradient = `<defs><linearGradient id="t" x1="0" y1="0" x2="1" y2="1">
  <stop offset="0" stop-color="#139f8f"/><stop offset="1" stop-color="#0b655d"/></linearGradient></defs>`;

const rounded = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">${gradient}
  <rect width="64" height="64" rx="16" fill="url(#t)"/>${bars(1, 0)}</svg>`;

// Full bleed: the bars shrink to 80% around the centre so masks never clip them.
const fullBleed = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">${gradient}
  <rect width="64" height="64" fill="url(#t)"/>${bars(0.8, 6.4)}</svg>`;

const icons = [
  ['icon-192.png', rounded, 192],
  ['icon-512.png', rounded, 512],
  ['icon-maskable-512.png', fullBleed, 512],
  ['apple-touch-icon.png', fullBleed, 180],
];

const browser = await chromium.launch();
const page = await browser.newPage();
for (const [name, svg, size] of icons) {
  await page.setViewportSize({ width: size, height: size });
  await page.setContent(
    `<style>html,body{margin:0;background:transparent}svg{display:block;width:${size}px;height:${size}px}</style>${svg}`,
  );
  await page.screenshot({ path: join(publicDir, name), omitBackground: true });
  console.log(`exported ${name}`);
}
await browser.close();

copyFileSync(join(here, 'logo/cadence-mark.svg'), join(publicDir, 'favicon.svg'));
console.log('copied favicon.svg');
