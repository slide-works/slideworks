import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { stubApi } from '../test-support/stubApi';

const route = { hooksConfig: { route: '/unsubscribe#t=abc_DEF-123' } };

test('unsubscribe page posts the fragment token on click, not on load', async ({ mount, page }) => {
  await stubApi(page, { '/api/email/unsubscribe': { status: 204 } });
  const posts: string[] = [];
  page.on('request', (request) => {
    if (request.url().endsWith('/api/email/unsubscribe')) posts.push(request.postData() ?? '');
  });
  await mount(<AppRoutes />, route);

  await expect(page.getByRole('heading', { name: 'Stop emails from SlideWorks?' })).toBeVisible();
  expect(posts).toEqual([]);
  await page.getByRole('button', { name: 'Stop emails' }).click();

  await expect(page.getByTestId('unsubscribe-done')).toContainText(
    'SlideWorks won’t email this address again.',
  );
  await expect(page.getByRole('heading', { name: 'Done' })).toBeFocused();
  expect(posts.map((body) => JSON.parse(body))).toEqual([{ token: 'abc_DEF-123' }]);
});

test('unsubscribe page shows the error state when the server answers 400', async ({
  mount,
  page,
}) => {
  await stubApi(page, {
    '/api/email/unsubscribe': { status: 400, json: { error: 'token: this link didn’t work' } },
  });
  await mount(<AppRoutes />, route);

  await page.getByRole('button', { name: 'Stop emails' }).click();

  await expect(page.getByRole('heading', { name: 'Link didn’t work' })).toBeFocused();
  await expect(page.getByTestId('unsubscribe-error')).toContainText(
    'Use the link in your latest email.',
  );
  await expect(page.getByRole('button', { name: 'Stop emails' })).toHaveCount(0);
});

test('unsubscribe page without a token shows the error state and no button', async ({
  mount,
  page,
}) => {
  await mount(<AppRoutes />, { hooksConfig: { route: '/unsubscribe' } });

  await expect(page.getByTestId('unsubscribe-error')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Stop emails' })).toHaveCount(0);
});

test('unsubscribe page keeps the button and shows the server text on a failure that is not a 400', async ({
  mount,
  page,
}) => {
  await stubApi(page, {
    '/api/email/unsubscribe': { status: 500, json: { error: 'The mail service is down' } },
  });
  await mount(<AppRoutes />, route);

  await page.getByRole('button', { name: 'Stop emails' }).click();

  await expect(page.getByTestId('unsubscribe-problem')).toHaveText('The mail service is down');
  await expect(page.getByRole('heading', { name: 'Link didn’t work' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Try again' })).toBeEnabled();
});

test('unsubscribe page says something went wrong when a failure has no text, and retry works', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/email/unsubscribe': { status: 503 } });
  await mount(<AppRoutes />, route);
  await page.getByRole('button', { name: 'Stop emails' }).click();
  await expect(page.getByTestId('unsubscribe-problem')).toHaveText(
    'Something went wrong, try again',
  );

  await stubApi(page, { '/api/email/unsubscribe': { status: 204 } });
  await page.getByRole('button', { name: 'Try again' }).click();

  await expect(page.getByTestId('unsubscribe-done')).toBeVisible();
});
