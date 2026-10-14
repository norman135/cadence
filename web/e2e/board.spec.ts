import { expect, test, type Locator, type Page } from '@playwright/test';
import {
  createIssue,
  createProject,
  signUpWithOrganization,
  uniqueSuffix,
} from './support/accounts';

/** Drags with real pointer moves, as dnd-kit's pointer sensor expects. */
async function drag(page: Page, source: Locator, target: Locator) {
  const from = await source.boundingBox();
  const to = await target.boundingBox();
  if (!from || !to) throw new Error('Drag source or target is not visible');
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
  await page.mouse.down();
  await page.mouse.move(from.x + from.width / 2 + 10, from.y + from.height / 2 + 10, { steps: 5 });
  await page.mouse.move(to.x + to.width / 2, to.y + 40, { steps: 15 });
  await page.mouse.up();
}

const column = (page: Page, name: string) => page.getByRole('list', { name: `${name} cards` });

test('a team works an issue across the board and shapes its workflow', async ({
  page,
  request,
}) => {
  await signUpWithOrganization(page, request, {
    name: 'Katherine Johnson',
    organization: `Orbits ${uniqueSuffix()}`,
  });
  await createProject(page, 'Mission Control', 'MC');
  await createIssue(page, 'Compute the launch window');
  await createIssue(page, 'Check the reentry angle');

  // The board shows the project's workflow as columns; new issues start in Todo.
  await page
    .getByRole('navigation', { name: 'Views' })
    .getByRole('link', { name: 'Board' })
    .click();
  await expect(column(page, 'Todo').getByRole('listitem')).toHaveCount(2);

  // Drag a card to In progress; the move sticks across a reload.
  await drag(
    page,
    column(page, 'Todo').getByRole('link', { name: 'Compute the launch window' }),
    column(page, 'In progress'),
  );
  await expect(
    column(page, 'In progress').getByRole('link', { name: 'Compute the launch window' }),
  ).toBeVisible();
  await page.reload();
  await expect(
    column(page, 'In progress').getByRole('link', { name: 'Compute the launch window' }),
  ).toBeVisible();
  await expect(column(page, 'Todo').getByRole('listitem')).toHaveCount(1);

  // Rename a status and give it a WIP limit; the board follows.
  await page.getByRole('main').getByRole('link', { name: 'Settings' }).click();
  await page.getByLabel('Name of In progress').fill('Calculating');
  await page.getByLabel('WIP limit of Calculating').fill('1');
  await page.getByRole('button', { name: 'Save workflow' }).click();
  await expect(page.getByText('Workflow saved')).toBeVisible();

  await page.goBack();
  await expect(page.getByRole('heading', { level: 2, name: 'Calculating' })).toBeVisible();
  await expect(page.getByTitle('Work-in-progress limit')).toHaveText('1 / 1');
});
