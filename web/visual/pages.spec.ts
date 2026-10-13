import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { mockApi, organization } from './mock-api';

/*
 * Each key page, in both themes: a screenshot compared with the committed baseline (so the app
 * can't drift from design/ unnoticed) and an axe scan (no serious or critical violations).
 *
 * Baselines are Linux renders; update them with `npm run test:visual:update`, which runs in the
 * same container image as CI.
 */

/** `settled` names something that appears once lazy content (lists, Markdown) has loaded. */
const pages: { name: string; path: string; signedIn: boolean; ready: string; settled?: string }[] =
  [
    { name: 'sign-in', path: '/login', signedIn: false, ready: 'Welcome back' },
    { name: 'register', path: '/register', signedIn: false, ready: 'Create your account' },
    {
      name: 'my-work',
      path: `/${organization.slug}`,
      signedIn: true,
      ready: 'Needs you',
      settled: 'text=Offline edits are lost when the app is killed',
    },
    {
      name: 'issues',
      path: `/${organization.slug}/projects/ATL`,
      signedIn: true,
      ready: 'Atlas Mobile',
      settled: 'text=Haptics on drag and drop',
    },
    {
      name: 'issue',
      path: `/${organization.slug}/issues/ATL-142`,
      signedIn: true,
      ready: 'Comments',
      settled: 'strong >> text=force-quit',
    },
    {
      name: 'members',
      path: `/${organization.slug}/settings/members`,
      signedIn: true,
      ready: 'Team',
    },
    {
      name: 'profile',
      path: `/${organization.slug}/settings/profile`,
      signedIn: true,
      ready: 'Appearance',
    },
  ];

async function open(page: Page, path: string, signedIn: boolean, ready: string, settled?: string) {
  // A fixed morning, so dates and greetings don't change the screenshot.
  await page.clock.setFixedTime(new Date('2026-09-30T09:30:00'));
  await mockApi(page, { signedIn });
  await page.goto(path);
  // The dev server compiles each route on first request, so allow for a cold start.
  await expect(page.getByRole('heading', { name: ready }).first()).toBeVisible({ timeout: 20_000 });
  // Account pages load their decorative brand panel lazily; wait for it on wide screens.
  if (!signedIn && (page.viewportSize()?.width ?? 0) >= 1024) {
    await expect(page.getByText('Self-hosted: your data stays on your server')).toBeVisible();
  }
  if (settled) await expect(page.locator(settled).first()).toBeVisible();
  await page.evaluate(() => document.fonts.ready);
}

const mySettled = 'text=Offline edits are lost when the app is killed';

async function expectAccessible(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
    .analyze();
  const serious = results.violations
    .filter((violation) => violation.impact === 'serious' || violation.impact === 'critical')
    .map((violation) => ({
      rule: violation.id,
      help: violation.help,
      targets: violation.nodes.map((node) => node.target.join(' ')),
    }));
  expect(serious).toEqual([]);
}

for (const scheme of ['light', 'dark'] as const) {
  test.describe(`${scheme} theme`, () => {
    test.use({ colorScheme: scheme });

    for (const { name, path, signedIn, ready, settled } of pages) {
      test(name, async ({ page }) => {
        await open(page, path, signedIn, ready, settled);
        await expect(page).toHaveScreenshot(`${name}-${scheme}.png`, { fullPage: true });
        await expectAccessible(page);
      });
    }

    test('command palette', async ({ page }) => {
      await open(page, `/${organization.slug}`, true, 'Needs you', mySettled);
      await page.keyboard.press('Control+k');
      await expect(page.getByPlaceholder('Search or type a command…')).toBeFocused();
      await expect(page).toHaveScreenshot(`command-palette-${scheme}.png`);
      await expectAccessible(page);
    });
  });
}

test.describe('phone', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test('my work with the tab bar', async ({ page }) => {
    await open(page, `/${organization.slug}`, true, 'Needs you', mySettled);
    await expect(page).toHaveScreenshot('my-work-phone.png', { fullPage: true });
  });

  test('navigation drawer', async ({ page }) => {
    await open(page, `/${organization.slug}`, true, 'Needs you', mySettled);
    await page.getByRole('button', { name: 'Open navigation' }).click();
    await expect(page.getByRole('dialog', { name: 'Navigation' })).toBeVisible();
    await expect(page).toHaveScreenshot('navigation-drawer-phone.png');
    await expectAccessible(page);
  });
});

test('the style guide matches in both themes', async ({ page }) => {
  await mockApi(page, { signedIn: false });
  await page.goto('/style-guide');
  await page.evaluate(() => document.fonts.ready);
  await expect(page).toHaveScreenshot('style-guide.png', { fullPage: true });
});
