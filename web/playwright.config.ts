import { defineConfig, devices } from '@playwright/test';

/*
 * End-to-end tests run against the production Docker Compose stack (Caddy, app, PostgreSQL)
 * plus Mailpit, which catches outgoing email:
 *
 *   cd deploy
 *   docker compose -f docker-compose.yml -f docker-compose.build.yml -f docker-compose.e2e.yml up -d --build
 *   cd ../web && npm run e2e
 *
 * E2E_BASE_URL and E2E_MAILPIT_URL point the tests at another deployment.
 */
export default defineConfig({
  testDir: './e2e',
  // Each test creates its own user, so tests are independent; the stack is shared, so a few
  // workers are enough.
  fullyParallel: true,
  workers: process.env.CI ? 2 : undefined,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'https://localhost',
    // Local and CI stacks use Caddy's self-signed certificate for "localhost".
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
