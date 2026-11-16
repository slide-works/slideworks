import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from './AppRoutes';
import { LocationProbe } from './test-support/LocationProbe';
import { adminUsers, me } from './test-support/fixtures/screens';
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

test('pending person on /decks lands on /pending', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: { ...me, access: 'pending' } } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/decks' } },
  );
  await expect(page.getByTestId('pending-page')).toBeAttached();
  await expect(page.getByTestId('location')).toHaveText('/pending');
});

test('approved person stays on /decks', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/decks' } },
  );
  await expect(page.getByTestId('decks-page')).toBeVisible();
  await expect(page.getByTestId('location')).toHaveText('/decks');
});

test('/admin/users renders inside the shell', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/me': { json: { ...me, isAdmin: true } },
    '/api/admin/users': { json: adminUsers },
  });
  await mount(<AppRoutes />, { hooksConfig: { route: '/admin/users' } });
  await expect(page.getByTestId('app-shell')).toBeVisible();
  await expect(page.getByTestId('admin-users-page')).toBeVisible();
  await expect(page.getByTestId('admin-user-row')).toHaveCount(adminUsers.users.length);
});

test('/pending renders without the shell', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: { ...me, access: 'pending' } } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/pending' } });
  await expect(page.getByTestId('pending-page')).toBeVisible();
  await expect(page.getByTestId('app-shell')).toHaveCount(0);
});

test('render route renders without sign-in', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { status: 401, json: { error: 'Not signed in' } } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/render/7Kq2xYp9LmQa/3#token=abc.def' } },
  );
  const slide = page.getByTestId('render-slide');
  await expect(slide).toBeVisible();
  await expect(slide).toHaveAttribute('data-deck-id', '7Kq2xYp9LmQa');
  await expect(slide).toHaveAttribute('data-slide', '3');
  await expect(slide).not.toHaveAttribute('data-error');
  await expect(page.getByTestId('location')).toHaveText('/render/7Kq2xYp9LmQa/3#token=abc.def');
});

test('render route without a token in the fragment reports it', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { status: 401, json: { error: 'Not signed in' } } });
  await mount(<AppRoutes />, {
    hooksConfig: { route: '/render/7Kq2xYp9LmQa/3?token=abc.def' },
  });
  await expect(page.getByTestId('render-slide')).toHaveAttribute(
    'data-error',
    'Render token missing',
  );
});

test('generic card route renders without sign-in or token', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { status: 401, json: { error: 'Not signed in' } } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: '/card/generic' } },
  );
  const card = page.getByTestId('share-card');
  await expect(card).toBeVisible();
  await expect(card).toHaveAttribute('data-generic', 'true');
  await expect(card).not.toHaveAttribute('data-error');
  await expect(page.getByTestId('location')).toHaveText('/card/generic');
});

test("a deck's card route renders without sign-in", async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { status: 401, json: { error: 'Not signed in' } } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/card/7Kq2xYp9LmQa#token=abc.def' } });
  const card = page.getByTestId('share-card');
  await expect(card).toHaveAttribute('data-deck-id', '7Kq2xYp9LmQa');
  await expect(card).toHaveAttribute('data-generic', 'false');
  await expect(card).not.toHaveAttribute('data-error');
});
