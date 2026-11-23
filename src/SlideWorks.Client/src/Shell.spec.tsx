import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from './AppRoutes';
import { me, profile } from './test-support/fixtures/screens';
import { stubApi } from './test-support/stubApi';

const route = { hooksConfig: { route: '/profile' } };

test('with no zone saved the shell sends the browser zone once, as fromBrowser', async ({
  mount,
  page,
}) => {
  await stubApi(page, {
    '/api/me': { json: { ...me, timeZone: null } },
    '/api/me/profile': { json: profile },
  });
  const bodies: unknown[] = [];
  await page.route('**/api/me/time-zone', (route) => {
    bodies.push(route.request().postDataJSON());
    return route.fulfill({ json: profile });
  });
  await mount(<AppRoutes />, route);
  await expect(page.getByTestId('profile-identity-panel')).toBeVisible();

  await expect.poll(() => bodies.length).toBe(1);
  const browserZone = await page.evaluate(() => Intl.DateTimeFormat().resolvedOptions().timeZone);
  expect(bodies).toEqual([{ timeZone: browserZone, fromBrowser: true }]);
});

test('with a zone saved the shell sends nothing', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me }, '/api/me/profile': { json: profile } });
  const bodies: unknown[] = [];
  await page.route('**/api/me/time-zone', (route) => {
    bodies.push(route.request().postDataJSON());
    return route.fulfill({ json: profile });
  });
  await mount(<AppRoutes />, route);
  await expect(page.getByTestId('profile-identity-panel')).toBeVisible();

  expect(bodies).toEqual([]);
});
