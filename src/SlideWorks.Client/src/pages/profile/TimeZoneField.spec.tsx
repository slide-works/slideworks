import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { me, profile } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';

const route = { hooksConfig: { route: '/profile' } };
const bangkok = { id: 'Asia/Bangkok', label: '(UTC+07:00) Bangkok' };
const zones = [...profile.account.timeZones, bangkok];
const withZones = { ...profile, account: { ...profile.account, timeZones: zones } };

test('the select lists the zones, shows the saved one and the UTC footer hint', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/me': { json: me }, '/api/me/profile': { json: withZones } });
  await mount(<AppRoutes />, route);

  await expect(page.getByLabel('Time zone', { exact: true })).toHaveValue('Europe/London');
  await expect(page.getByRole('option')).toHaveCount(zones.length);
  await expect(page.getByTestId('profile-time-zone')).toContainText('Slide footers stay in UTC');
  await expect(page.getByRole('button', { name: 'Save time zone' })).toBeDisabled();
});

test('picking a zone and saving sends it and shows it saved', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me }, '/api/me/profile': { json: withZones } });
  const bodies: unknown[] = [];
  await page.route('**/api/me/time-zone', (route) => {
    bodies.push(route.request().postDataJSON());
    return route.fulfill({
      json: { ...withZones, account: { ...withZones.account, timeZone: bangkok.id } },
    });
  });
  await mount(<AppRoutes />, route);

  await page.getByLabel('Time zone', { exact: true }).selectOption('Asia/Bangkok');
  await page.getByRole('button', { name: 'Save time zone' }).click();

  await expect(page.getByLabel('Time zone', { exact: true })).toHaveValue('Asia/Bangkok');
  await expect(page.getByRole('button', { name: 'Save time zone' })).toBeDisabled();
  await expect(page.getByLabel('Time zone', { exact: true })).toBeFocused();
  expect(bodies).toEqual([{ timeZone: 'Asia/Bangkok', fromBrowser: false }]);
});

test('a refused zone shows the server text under the select and keeps focus there', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/me': { json: me }, '/api/me/profile': { json: withZones } });
  await page.route('**/api/me/time-zone', (route) =>
    route.fulfill({ status: 400, json: { error: 'That time zone is not one of the choices.' } }),
  );
  await mount(<AppRoutes />, route);

  await page.getByLabel('Time zone', { exact: true }).selectOption('Asia/Bangkok');
  await page.getByRole('button', { name: 'Save time zone' }).click();

  await expect(page.getByTestId('profile-time-zone-error')).toHaveText(
    'That time zone is not one of the choices.',
  );
  await expect(page.getByLabel('Time zone', { exact: true })).toBeFocused();
});

test('with no zone saved the select says Not set and Save stays off until one is picked', async ({
  mount,
  page,
}) => {
  const empty = { ...withZones, account: { ...withZones.account, timeZone: null } };
  await stubApi(page, {
    '/api/me': { json: { ...me, timeZone: null } },
    '/api/me/profile': { json: empty },
  });
  await page.route('**/api/me/time-zone', (route) => route.fulfill({ json: empty }));
  await mount(<AppRoutes />, route);

  await expect(page.getByLabel('Time zone', { exact: true })).toHaveValue('');
  await expect(page.getByRole('option', { name: 'Not set (UTC)' })).toHaveCount(1);
  await expect(page.getByRole('button', { name: 'Save time zone' })).toBeDisabled();
  await page.getByLabel('Time zone', { exact: true }).selectOption('Europe/London');
  await expect(page.getByRole('button', { name: 'Save time zone' })).toBeEnabled();
});
