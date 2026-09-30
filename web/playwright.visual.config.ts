import { defineConfig, devices } from '@playwright/test';

/*
 * Visual regression and accessibility checks for the key pages, against the Vite dev server with
 * a mocked API (visual/mock-api.ts). No backend is needed, and the result depends only on the UI.
 *
 * Screenshots differ between operating systems (font rendering), so the baselines are Linux
 * renders made in the same container image CI uses:
 *
 *   npm run test:visual            # compare (Linux / CI)
 *   npm run test:visual:update     # regenerate baselines in Docker, from any OS
 */
const port = 4173;

export default defineConfig({
  testDir: './visual',
  snapshotPathTemplate: '{testDir}/__screenshots__/{arg}{ext}',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  expect: {
    toHaveScreenshot: {
      // Anti-aliasing can differ by a few pixels between runs; layout or color changes can't hide
      // under this.
      maxDiffPixelRatio: 0.002,
      animations: 'disabled',
      caret: 'hide',
    },
  },
  use: {
    baseURL: `http://localhost:${String(port)}`,
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } },
    },
  ],
  webServer: {
    command: `npm run dev -- --port ${String(port)} --strictPort`,
    url: `http://localhost:${String(port)}`,
    reuseExistingServer: !process.env.CI,
    // Every API call is mocked in the browser; point the dev proxy at a closed port so a missing
    // fixture fails instead of reaching a real server.
    env: { CADENCE_API_URL: 'http://127.0.0.1:9' },
  },
});
