import { expect, type APIRequestContext, type Page } from '@playwright/test';
import { linkFromEmail } from './mailpit';

export const password = 'correct horse battery staple';

export const uniqueSuffix = () =>
  `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;

/** Signs up a new user, confirms their email, signs in and creates an organization, ending on My work. */
export async function signUpWithOrganization(
  page: Page,
  request: APIRequestContext,
  { name, organization }: { name: string; organization: string },
) {
  const email = `${name.split(' ')[0]?.toLowerCase() ?? 'user'}-${uniqueSuffix()}@cadence.test`;

  await page.goto('/register');
  await page.getByLabel('Name').fill(name);
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page.getByText('Check your email')).toBeVisible();
  await page.goto(await linkFromEmail(request, email, '/confirm-email'));
  await expect(page.getByText('Your email is confirmed')).toBeVisible();

  await page.goto('/login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await page.getByLabel('Organization name').fill(organization);
  await page.getByRole('button', { name: 'Create organization' }).click();
  await expect(page.getByRole('heading', { level: 2, name: 'Needs you' })).toBeVisible();
  return email;
}

/** Creates a project with a chosen key from the sidebar and waits for its issue list. */
export async function createProject(page: Page, name: string, key: string) {
  await page.getByRole('button', { name: 'New project' }).first().click();
  await page.getByLabel('Name').fill(name);
  await page.getByLabel('Key').fill(key);
  await page.getByRole('button', { name: 'Create project' }).click();
  await expect(page.getByRole('heading', { name })).toBeVisible();
}

/** Creates an issue from the top bar's New issue dialog. */
export async function createIssue(page: Page, title: string) {
  await page.getByRole('button', { name: 'New issue' }).first().click();
  const dialog = page.getByRole('dialog', { name: 'New issue' });
  await dialog.getByLabel('Title').fill(title);
  await dialog.getByRole('button', { name: 'Create issue' }).click();
  await expect(dialog).toBeHidden();
}
