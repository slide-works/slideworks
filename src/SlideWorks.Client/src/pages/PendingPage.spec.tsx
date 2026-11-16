import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { me } from '../test-support/fixtures/screens';
import { LocationProbe } from '../test-support/LocationProbe';
import { stubApi } from '../test-support/stubApi';

const route = { hooksConfig: { route: '/pending' } };
const pending = { ...me, access: 'pending', isAdmin: false, adminMissing: false };

test('pending page shows email and waiting message', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: pending } });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('pending-heading')).toHaveText('Waiting for approval');
  await expect(page.getByTestId('pending-page')).toContainText(
    'Your request is waiting for an admin to approve it.',
  );
  await expect(page.getByTestId('pending-email')).toHaveText(me.email);
  await expect(page.getByTestId('pending-sign-out')).toHaveText('Sign out');
});

test('pending page shows admin-missing note', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: { ...pending, adminMissing: true } } });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('pending-admin-missing')).toHaveText(
    'This server has no admin yet: set SLIDEWORKS_ADMIN_EMAIL.',
  );
});

test('pending page hides admin-missing note when an admin exists', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: pending } });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('pending-email')).toBeVisible();
  await expect(page.getByTestId('pending-admin-missing')).toHaveCount(0);
});

test('sign out posts and goes to /signin', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/me': { json: pending },
    '/api/signout': { status: 204 },
    '/api/signin/options': { json: { google: true } },
  });
  const signOut = page.waitForRequest(
    (request) => request.url().endsWith('/api/signout') && request.method() === 'POST',
  );
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    route,
  );

  await page.getByTestId('pending-sign-out').click();
  await signOut;
  await expect(page.getByTestId('location')).toHaveText('/signin');
});

test('sign out failure shows a message', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/me': { json: pending },
    '/api/signout': { status: 500, json: { error: 'boom' } },
  });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    route,
  );

  await page.getByTestId('pending-sign-out').click();
  await expect(page.getByTestId('pending-sign-out-error')).toHaveText(
    'Couldn’t sign out. Try again.',
  );
  await expect(page.getByTestId('location')).toHaveText('/pending');
});

test('approved person is sent to /decks', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: { ...me, access: 'approved' } } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    route,
  );

  await expect(page.getByTestId('location')).toHaveText('/decks');
});

test('signed-out person is sent to /signin', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/me': { status: 401, json: { error: 'Sign in required' } },
    '/api/signin/options': { json: { google: true } },
  });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    route,
  );

  await expect(page.getByTestId('location')).toHaveText('/signin');
});

test('unreachable server shows the message', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': 'network-down' });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('pending-unreachable')).toHaveText("Can't reach SlideWorks");
  await expect(page.getByTestId('pending-sign-out')).toHaveCount(0);
});

test('pending page matches its screenshot', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: { ...pending, adminMissing: true } } });
  await mount(<AppRoutes />, route);
  await expect(page.getByTestId('pending-email')).toBeVisible();

  await expect(page.getByTestId('pending-page')).toHaveScreenshot('pending-page.png');
});
