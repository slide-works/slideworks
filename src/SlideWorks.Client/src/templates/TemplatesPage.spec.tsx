import { test, expect } from '@playwright/experimental-ct-react';
import { templates } from '../test-support/fixtures/templates';
import { stubTemplatesApi } from '../test-support/stubTemplatesApi';
import { TemplatesPage } from './TemplatesPage';
import { TEMPLATES_TEST_IDS } from './testIds';

const route = { hooksConfig: { route: '/templates' } };

test('page loads the list', async ({ mount, page }) => {
  const requests = await stubTemplatesApi(page);
  await mount(<TemplatesPage />, route);

  await expect(page.getByRole('heading', { name: 'Templates' })).toBeVisible();
  const mine = page.getByRole('list', { name: 'Your templates' });
  await expect(mine.getByTestId(TEMPLATES_TEST_IDS.rowName)).toHaveText(
    templates.mine.map((row) => row.name),
  );
  await expect(
    page.getByRole('list', { name: 'Shared with you' }).getByTestId(TEMPLATES_TEST_IDS.rowName),
  ).toHaveText(['Harbour talks']);
  await expect(
    page.getByRole('list', { name: 'Built in' }).getByTestId(TEMPLATES_TEST_IDS.rowName),
  ).toHaveText(['Workshop']);
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.error)).toHaveCount(0);
  expect(requests).toEqual([{ call: 'list', id: null, body: null }]);
});

test('shows a message when the list fails', async ({ mount, page }) => {
  await stubTemplatesApi(page, {
    list: { status: 500, json: { error: 'Something went wrong. Try again.' } },
  });
  await mount(<TemplatesPage />, route);

  const error = page.getByTestId(TEMPLATES_TEST_IDS.error);
  await expect(error).toHaveText('Something went wrong. Try again.');
  await expect(error).toHaveAttribute('role', 'alert');
  await expect(page.getByTestId(TEMPLATES_TEST_IDS.row)).toHaveCount(0);
});

test('shows a message when the server cannot be reached', async ({ mount, page }) => {
  await stubTemplatesApi(page, { list: 'network-down' });
  await mount(<TemplatesPage />, route);

  await expect(page.getByTestId(TEMPLATES_TEST_IDS.error)).toHaveText("Can't reach SlideWorks.");
});
