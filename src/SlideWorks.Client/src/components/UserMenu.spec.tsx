import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { LocationProbe } from '../test-support/LocationProbe';
import { decksLibrary, me } from '../test-support/fixtures/screens';
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

test('user menu lists People (admin) for an admin and not for anyone else', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/me': { json: { ...me, isAdmin: true } } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/decks' } },
  );
  await page.getByTestId('user-menu-button').click();
  await expect(page.getByTestId('user-menu-people')).toHaveText('People (admin)');
  await expect(page.getByTestId('user-menu-people')).toHaveAttribute('href', '/admin/users');
});

test('user menu has no People (admin) for a non-admin', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: { ...me, isAdmin: false } } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/decks' } });
  await page.getByTestId('user-menu-button').click();

  await expect(page.getByTestId('user-menu-profile')).toBeVisible();
  await expect(page.getByTestId('user-menu-people')).toHaveCount(0);
});

test.describe('user menu panel', () => {
  test.beforeEach(async ({ mount, page }) => {
    await stubApi(page, {
      '/api/me': { json: me },
      '/api/decks': { json: decksLibrary },
    });
    await mount(
      <LocationProbe>
        <AppRoutes />
      </LocationProbe>,
      { hooksConfig: { route: '/decks' } },
    );
    await page.getByTestId('user-menu-button').click();
    await expect(page.getByTestId('user-menu-panel')).toBeVisible();
  });

  test('opening the user menu moves focus to Profile, which goes to /profile', async ({ page }) => {
    await expect(page.getByTestId('user-menu-profile')).toBeFocused();

    await page.getByTestId('user-menu-profile').click();

    await expect(page.getByTestId('location')).toHaveText('/profile');
    await expect(page.getByTestId('user-menu-panel')).toHaveCount(0);
  });

  test('escape closes the user menu and returns focus to its button', async ({ page }) => {
    await page.keyboard.press('Escape');

    await expect(page.getByTestId('user-menu-panel')).toHaveCount(0);
    await expect(page.getByTestId('user-menu-button')).toBeFocused();
    await expect(page.getByTestId('user-menu-button')).toHaveAttribute('aria-expanded', 'false');
  });

  test('a click inside the user menu keeps it open and a click outside closes it', async ({
    page,
  }) => {
    await page.getByTestId('user-menu-panel').getByText(me.email).click();
    await expect(page.getByTestId('user-menu-panel')).toBeVisible();

    await page.locator('main').click({ position: { x: 4, y: 4 } });

    await expect(page.getByTestId('user-menu-panel')).toHaveCount(0);
  });

  test('tabbing out of the user menu closes it', async ({ page }) => {
    await page.keyboard.press('Shift+Tab');
    await expect(page.getByTestId('user-menu-button')).toBeFocused();
    await expect(page.getByTestId('user-menu-panel')).toBeVisible();

    await page.keyboard.press('Tab');
    await expect(page.getByTestId('user-menu-profile')).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('user-menu-signout')).toBeFocused();
    await page.keyboard.press('Tab');

    await expect(page.getByTestId('user-menu-panel')).toHaveCount(0);
  });
});
