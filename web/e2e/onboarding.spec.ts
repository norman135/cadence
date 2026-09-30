import { expect, test, type Page } from '@playwright/test';
import { linkFromEmail } from './support/mailpit';

const password = 'correct horse battery staple';

function uniqueSuffix() {
  return `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

async function createOrganization(page: Page, name: string) {
  await page.getByLabel('Organization name').fill(name);
  await page.getByRole('button', { name: 'Create organization' }).click();
  await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();
}

test('a new user signs up, confirms their email and sets up two organizations', async ({
  page,
  request,
}) => {
  const suffix = uniqueSuffix();
  const email = `ada-${suffix}@cadence.test`;
  const first = `Analytical Engines ${suffix}`;
  const second = `Difference Engines ${suffix}`;

  // Sign up: confirmation is required, so no session yet.
  await page.goto('/register');
  await page.getByLabel('Name').fill('Ada Lovelace');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page.getByText('Check your email')).toBeVisible();

  // Confirm through the link in the real email.
  await page.goto(await linkFromEmail(request, email, '/confirm-email'));
  await expect(page.getByText('Your email is confirmed')).toBeVisible();
  await page.getByRole('link', { name: 'Sign in' }).click();

  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();

  // Without an organization, onboarding asks for one.
  await expect(page.getByText('Welcome, Ada Lovelace!')).toBeVisible();
  await createOrganization(page, first);
  const firstUrl = page.url();

  const switcher = page.getByRole('button', { name: 'Switch organization' }).first();
  await switcher.click();
  await page.getByRole('menuitem', { name: 'New organization' }).click();
  await createOrganization(page, second);

  // Switch back through the organization switcher.
  await switcher.click();
  await page.getByRole('menuitem', { name: first }).click();
  await expect(page.getByRole('heading', { level: 1, name: first })).toBeVisible();
  expect(page.url()).toBe(firstUrl);

  // A reload restores the session from the refresh cookie.
  await page.reload();
  await expect(page.getByRole('heading', { level: 1, name: first })).toBeVisible();

  // Signing out ends the session; protected pages send the visitor back to sign in.
  await page.getByRole('button', { name: 'Account menu' }).click();
  await page.getByRole('menuitem', { name: 'Sign out' }).click();
  await expect(page.getByText('Sign in to Cadence')).toBeVisible();

  await page.goto(firstUrl);
  await expect(page.getByText('Sign in to Cadence')).toBeVisible();
});

test('signing in before confirming the email is refused', async ({ page }) => {
  const email = `unconfirmed-${uniqueSuffix()}@cadence.test`;

  await page.goto('/register');
  await page.getByLabel('Name').fill('Charles Babbage');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page.getByText('Check your email')).toBeVisible();

  await page.goto('/login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();

  const alert = page.getByRole('alert');
  await expect(alert).toContainText('Confirm your email address first.');
  await expect(page).toHaveURL(/\/login/);

  await alert.getByRole('button', { name: 'Send the link again' }).click();
  await expect(alert).toContainText('We sent a new link.');
});
