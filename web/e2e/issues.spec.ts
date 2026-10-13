import { expect, test } from '@playwright/test';
import { linkFromEmail } from './support/mailpit';

const password = 'correct horse battery staple';

test('a team lead creates a project, files an issue and discusses it', async ({
  page,
  request,
}) => {
  const suffix = `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
  const email = `grace-${suffix}@cadence.test`;

  // A confirmed user with an organization.
  await page.goto('/register');
  await page.getByLabel('Name').fill('Grace Hopper');
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
  await page.getByLabel('Organization name').fill(`Compilers ${suffix}`);
  await page.getByRole('button', { name: 'Create organization' }).click();
  await expect(page.getByRole('heading', { level: 2, name: 'Needs you' })).toBeVisible();

  // A project, with its key chosen rather than suggested.
  await page.getByRole('button', { name: 'New project' }).first().click();
  await page.getByLabel('Name').fill('Engine Room');
  await page.getByLabel('Key').fill('ENG');
  await page.getByRole('button', { name: 'Create project' }).click();
  await expect(page.getByRole('heading', { name: 'Engine Room' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Projects' })).toContainText('Engine Room');

  // An issue gets the project's next key and shows up in the list.
  await page.getByRole('button', { name: 'New issue' }).first().click();
  const dialog = page.getByRole('dialog', { name: 'New issue' });
  await dialog.getByLabel('Title').fill('Compiler rejects nested loops');
  await dialog.getByLabel('Description').fill('Fails on **three** levels of nesting.');
  await dialog.getByRole('button', { name: 'Create issue' }).click();
  await expect(dialog).toBeHidden();
  const row = page.getByRole('link', { name: /Compiler rejects nested loops/ });
  await expect(row).toContainText('ENG-1');

  // It opens in a drawer over the list, and from there on its own page.
  await row.click();
  const drawer = page.getByRole('dialog', { name: 'ENG-1' });
  await expect(drawer.locator('strong', { hasText: 'three' })).toBeVisible();
  await drawer.getByRole('link', { name: 'Open full page' }).click();
  await expect(page).toHaveURL(/\/issues\/ENG-1$/);

  // Changing a property sticks across a reload.
  await page.getByRole('button', { name: 'Status' }).click();
  await page.getByRole('menuitemradio', { name: 'In progress' }).click();
  await expect(page.getByRole('button', { name: 'Status' })).toContainText('In progress');
  await page.reload();
  await expect(page.getByRole('button', { name: 'Status' })).toContainText('In progress');

  // Comments render Markdown.
  await page.getByLabel('Leave a comment').fill('Reproduced with `for` inside `while`.');
  await page.getByRole('button', { name: 'Comment', exact: true }).click();
  await expect(page.locator('code', { hasText: 'while' })).toBeVisible();

  // The command palette finds the issue by key.
  await page.getByRole('link', { name: 'All issues' }).first().click();
  await page.keyboard.press('Control+k');
  await page.getByPlaceholder('Search or type a command…').fill('ENG-1');
  await expect(page.getByRole('option', { name: /Compiler rejects nested loops/ })).toBeVisible();
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/issues\/ENG-1$/);
});
