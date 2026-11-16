import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { LocationProbe } from '../test-support/LocationProbe';
import { me } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';

test('user menu signs out and goes to sign-in', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/me': { json: me },
    '/api/signin/options': { json: { google: true } },
  });
  const signOuts: string[] = [];
  await page.route('**/api/signout', (route) => {
    signOuts.push(route.request().method());
    return route.fulfill({ status: 204 });
  });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/decks' } },
  );
  await expect(page.getByTestId('user-menu')).toHaveText('SR');

  await page.getByTestId('user-menu-button').click();
  await expect(page.getByTestId('user-menu-panel')).toContainText(me.email);
  await page.getByTestId('user-menu-signout').click();

  await expect(page.getByTestId('signin-page')).toBeVisible();
  await expect(page.getByTestId('location')).toHaveText('/signin');
  expect(signOuts).toEqual(['POST']);
});

test('user menu keeps the person signed in when sign-out fails', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/me': { json: me },
    '/api/signout': { status: 403, json: { error: 'Cross-site request refused' } },
  });
  await mount(<AppRoutes />, { hooksConfig: { route: '/decks' } });

  await page.getByTestId('user-menu-button').click();
  await page.getByTestId('user-menu-signout').click();

  await expect(page.getByTestId('user-menu-error')).toHaveText('Couldn’t sign out. Try again.');
  await expect(page.getByTestId('app-shell')).toBeVisible();
});
