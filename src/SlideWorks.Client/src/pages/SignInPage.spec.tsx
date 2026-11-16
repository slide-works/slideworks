import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { stubApi } from '../test-support/stubApi';

const googleOn = { '/api/signin/options': { json: { google: true } } };

test('sign-in page shows Google button', async ({ mount, page }) => {
  await stubApi(page, googleOn);
  const returnTo = '/oauth/authorize?client_id=abc&state=x#top';
  await mount(<AppRoutes />, {
    hooksConfig: { route: `/signin?returnTo=${encodeURIComponent(returnTo)}` },
  });
  const button = page.getByTestId('signin-google');
  await expect(button).toHaveText('Sign in with Google');
  await expect(button).toHaveAttribute(
    'href',
    `/signin/google?returnTo=${encodeURIComponent(returnTo)}`,
  );
  await expect(page.getByTestId('signin-error')).toHaveCount(0);
  await expect(page.getByTestId('signin-not-set-up')).toHaveCount(0);
});

test('request-access hint shown', async ({ mount, page }) => {
  await stubApi(page, googleOn);
  await mount(<AppRoutes />, { hooksConfig: { route: '/signin' } });
  await expect(page.getByTestId('signin-request-hint')).toHaveText(
    'New here? Sign in with Google to request access.',
  );
  await expect(page.getByTestId('signin-google')).toHaveAttribute(
    'href',
    `/signin/google?returnTo=${encodeURIComponent('/')}`,
  );
});

test('not configured shows message', async ({ mount, page }) => {
  await stubApi(page, { '/api/signin/options': { json: { google: false } } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/signin?returnTo=%2Fdecks' } });
  await expect(page.getByTestId('signin-not-set-up')).toHaveText(
    'Sign-in isn’t set up on this server.',
  );
  await expect(page.getByTestId('signin-google')).toHaveCount(0);
  await expect(page.getByTestId('signin-request-hint')).toHaveCount(0);
});

test('error query shows message', async ({ mount, page }) => {
  await stubApi(page, googleOn);
  await mount(<AppRoutes />, {
    hooksConfig: { route: '/signin?error=email_linked&returnTo=%2Fdecks' },
  });
  await expect(page.getByTestId('signin-error')).toHaveText(
    'This email is linked to another Google account.',
  );
  // Trying again still carries returnTo.
  await expect(page.getByTestId('signin-google')).toHaveAttribute(
    'href',
    '/signin/google?returnTo=%2Fdecks',
  );
});

test('not_approved message shown', async ({ mount, page }) => {
  await stubApi(page, googleOn);
  await mount(<AppRoutes />, { hooksConfig: { route: '/signin?error=not_approved' } });
  await expect(page.getByTestId('signin-error')).toHaveText(
    'An admin hasn’t approved this account on this server.',
  );
});

test('unknown error code shows the generic message', async ({ mount, page }) => {
  await stubApi(page, googleOn);
  await mount(<AppRoutes />, { hooksConfig: { route: '/signin?error=%3Cscript%3E' } });
  await expect(page.getByTestId('signin-error')).toHaveText('Sign-in didn’t work. Try again.');
});

test('options unreachable says so', async ({ mount, page }) => {
  await stubApi(page, { '/api/signin/options': 'network-down' });
  await mount(<AppRoutes />, { hooksConfig: { route: '/signin' } });
  await expect(page.getByTestId('signin-unreachable')).toBeVisible();
  await expect(page.getByTestId('signin-google')).toHaveCount(0);
});
