import { test, expect } from '@playwright/experimental-ct-react';
import { me } from '../test-support/fixtures/screens';
import { AdminLink } from './AdminLink';

const route = { hooksConfig: { route: '/decks' } };

test('admin link hidden for non-admin', async ({ mount, page }) => {
  await mount(<AdminLink me={{ ...me, isAdmin: false, pendingRequests: 0 }} />, route);

  await expect(page.getByTestId('admin-link')).toHaveCount(0);
});

test('admin link is a plain link when nobody is waiting', async ({ mount, page }) => {
  await mount(<AdminLink me={{ ...me, isAdmin: true, pendingRequests: 0 }} />, route);

  await expect(page.getByTestId('admin-link')).toHaveText('Admin');
  await expect(page.getByTestId('admin-link')).toHaveAttribute('href', '/admin/users');
  await expect(page.getByTestId('admin-link-badge')).toHaveCount(0);
});

test('admin link shows count badge', async ({ mount, page }) => {
  await mount(<AdminLink me={{ ...me, isAdmin: true, pendingRequests: 3 }} />, route);

  await expect(page.getByTestId('admin-link')).toHaveText('Admin3');
  await expect(page.getByTestId('admin-link-badge')).toHaveText('3');
});

test('admin link badge matches its screenshot', async ({ mount, page }) => {
  await mount(<AdminLink me={{ ...me, isAdmin: true, pendingRequests: 3 }} />, route);

  await expect(page.getByTestId('admin-link')).toHaveScreenshot('admin-link-badge.png');
});
