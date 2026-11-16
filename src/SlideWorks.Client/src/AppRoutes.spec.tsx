import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from './AppRoutes';
import { LocationProbe } from './test-support/LocationProbe';
import { me } from './test-support/fixtures/screens';
import { stubApi } from './test-support/stubApi';

test('signed out → /signin', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { status: 401, json: { error: 'Not signed in' } } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/oauth/consent?request=req_123#top' } },
  );
  await expect(page.getByTestId('signin-page')).toBeVisible();
  await expect(page.getByTestId('location')).toHaveText(
    `/signin?returnTo=${encodeURIComponent('/oauth/consent?request=req_123#top')}`,
  );
});

test('signed in → decks shell', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/' } });
  await expect(page.getByTestId('app-shell')).toBeVisible();
  await expect(page.getByTestId('decks-page')).toBeVisible();
  await expect(page.getByTestId('user-menu')).toHaveText('SR');
  await expect(page.getByRole('link', { name: 'Decks' })).toBeVisible();
});

test('api down shows cannot reach', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': 'network-down' });
  await mount(<AppRoutes />, { hooksConfig: { route: '/decks' } });
  await expect(page.getByTestId('app-unreachable')).toContainText("Can't reach SlideWorks");
  await expect(page.getByTestId('app-shell')).toHaveCount(0);
});

test('player route renders without sign-in', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { status: 401, json: { error: 'Not signed in' } } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/d/7Kq2xYp9LmQa/ship-smaller' } },
  );
  await expect(page.getByTestId('player-page')).toBeVisible();
  await expect(page.getByTestId('location')).toHaveText('/d/7Kq2xYp9LmQa/ship-smaller');
});
