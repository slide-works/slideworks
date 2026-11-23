import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { me, profile } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';

const route = { hooksConfig: { route: '/profile' } };

test('profile page renders the three panels from the profile', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me }, '/api/me/profile': { json: profile } });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('profile-identity-panel')).toBeVisible();
  await expect(page.getByTestId('profile-name')).toHaveText(profile.identity.name);
  await expect(page.getByTestId('profile-email')).toHaveText(profile.identity.email);
  await expect(page.getByTestId('profile-photo-source')).toHaveText('Picture: Your photo');
  await expect(page.getByTestId('profile-account-panel')).toBeVisible();
  await expect(page.getByTestId('profile-time-zone')).toHaveText('(UTC+00:00) London');
  await expect(page.getByTestId('profile-agents')).toBeVisible();
  await expect(page.getByTestId('profile-agent')).toHaveCount(2);
  await expect(page.getByTestId('profile-agent-connected').nth(1)).toHaveText('Connected: Unknown');
  await expect(page.getByTestId('profile-agent-name').first()).toHaveText('Claude');
});

test('profile page shows Admin and the admin link for an admin', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me }, '/api/me/profile': { json: profile } });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('profile-access')).toHaveText('Access: Admin');
  await expect(page.getByTestId('profile-admin-link')).toHaveAttribute('href', '/admin/users');
});

test('profile page shows neither Admin nor the admin link for a non-admin', async ({
  mount,
  page,
}) => {
  const member = { ...profile, account: { ...profile.account, isAdmin: false, adminUrl: null } };
  await stubApi(page, { '/api/me': { json: me }, '/api/me/profile': { json: member } });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('profile-access')).toHaveText('Access: Member');
  await expect(page.getByTestId('profile-admin-link')).toHaveCount(0);
});

test('profile page shows the server message when the profile fails to load', async ({
  mount,
  page,
}) => {
  await stubApi(page, {
    '/api/me': { json: me },
    '/api/me/profile': { status: 500, json: { error: 'Something went wrong' } },
  });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('profile-error')).toHaveText('Something went wrong');
});
