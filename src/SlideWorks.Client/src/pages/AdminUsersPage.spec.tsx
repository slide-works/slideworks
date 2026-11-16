import { test, expect } from '@playwright/experimental-ct-react';
import type { AdminUsersView } from '../api/types';
import { adminUsers } from '../test-support/fixtures/screens';
import { PendingHandlerSilenced } from '../test-support/PendingHandlerSilenced';
import { stubApi } from '../test-support/stubApi';
import { AdminUsersPage } from './AdminUsersPage';

const route = { hooksConfig: { route: '/admin/users' } };
const [pat, ada, sam, mira] = adminUsers.users;

const row = (page: import('playwright/test').Page, id: string) =>
  page.locator(`[data-user-id="${id}"]`);

test('lists pending first with approve and reject', async ({ mount, page }) => {
  await stubApi(page, { '/api/admin/users': { json: adminUsers } });
  await mount(<AdminUsersPage />, route);

  const rows = page.getByTestId('admin-user-row');
  await expect(rows).toHaveCount(4);
  await expect(rows.nth(0).getByTestId('admin-user-name')).toHaveText('Pat Lindqvist');
  await expect(rows.nth(0).getByTestId('admin-user-email')).toHaveText('pat@pending.example');
  await expect(rows.nth(0).getByTestId('admin-user-access')).toHaveText('pending');
  await expect(rows.nth(0).getByTestId('admin-approve')).toBeVisible();
  await expect(rows.nth(0).getByTestId('admin-reject')).toBeVisible();
  await expect(rows.nth(3).getByTestId('admin-user-name')).toHaveText('Mira Okafor');
  await expect(page.getByTestId('admin-pending-count')).toHaveText('1 waiting');
});

test('shows the right buttons for each access state', async ({ mount, page }) => {
  await stubApi(page, { '/api/admin/users': { json: adminUsers } });
  await mount(<AdminUsersPage />, route);

  const buttons = async (id: string) =>
    row(page, id)
      .getByRole('button')
      .evaluateAll((all) => all.map((button) => button.textContent));

  await expect(row(page, pat.id)).toBeVisible();
  expect(await buttons(pat.id)).toEqual(['Approve', 'Reject']);
  expect(await buttons(ada.id)).toEqual(['Remove admin', 'Revoke']);
  expect(await buttons(sam.id)).toEqual(['Make admin', 'Revoke']);
  expect(await buttons(mira.id)).toEqual(['Approve']);
});

test('pending rows say when requested and decided rows say who decided', async ({
  mount,
  page,
}) => {
  const view: AdminUsersView = {
    ...adminUsers,
    users: [{ ...pat, changedLabel: null, changedBy: null }, { ...sam, changedBy: null }, mira],
  };
  await stubApi(page, { '/api/admin/users': { json: view } });
  await mount(<AdminUsersPage />, route);

  await expect(row(page, pat.id).getByTestId('admin-user-when')).toHaveText('requested 2 min ago');
  await expect(row(page, sam.id).getByTestId('admin-user-when')).toHaveText('Yesterday');
  await expect(row(page, mira.id).getByTestId('admin-user-when')).toHaveText(
    '1 h ago by Ada Brooks',
  );
});

test('approve replaces the view', async ({ mount, page }) => {
  const approved: AdminUsersView = {
    pendingCount: 0,
    adminCount: 1,
    users: [{ ...ada }, { ...pat, access: 'approved' }, sam, mira],
  };
  await stubApi(page, { '/api/admin/users': { json: adminUsers } });
  const posts: { method: string; body: string | null }[] = [];
  let lists = 0;
  await page.route('**/api/admin/users', (route) => {
    lists += 1;
    return route.fulfill({ json: adminUsers });
  });
  await page.route(`**/api/admin/users/${pat.id}/approve`, (route) => {
    posts.push({ method: route.request().method(), body: route.request().postData() });
    return route.fulfill({ json: approved });
  });
  await mount(<AdminUsersPage />, route);

  await row(page, pat.id).getByTestId('admin-approve').click();

  await expect(page.getByTestId('admin-pending-count')).toHaveText('0 waiting');
  await expect(page.getByTestId('admin-user-row').nth(0).getByTestId('admin-user-name')).toHaveText(
    'Ada Brooks (you)',
  );
  await expect(row(page, pat.id).getByTestId('admin-user-access')).toHaveText('approved');
  expect(posts).toEqual([{ method: 'POST', body: null }]);
  expect(lists).toBe(1);
  await expect(row(page, pat.id).getByTestId('admin-make-admin')).toBeFocused();
});

for (const [status, message] of [
  [409, "The last admin can't be removed"],
  [404, 'User not found'],
] as const) {
  test(`${status} shows the server message on the row`, async ({ mount, page }) => {
    await stubApi(page, { '/api/admin/users': { json: adminUsers } });
    await page.route(`**/api/admin/users/${ada.id}/remove-admin`, (route) =>
      route.fulfill({ status, json: { error: message } }),
    );
    await mount(<AdminUsersPage />, route);

    await row(page, ada.id).getByTestId('admin-remove-admin').click();

    await expect(row(page, ada.id).getByTestId('admin-user-error')).toHaveText(message);
    await expect(page.getByTestId('admin-user-row')).toHaveCount(4);
    await expect(row(page, sam.id).getByTestId('admin-user-error')).toHaveCount(0);
    await expect(row(page, ada.id).getByTestId('admin-remove-admin')).toBeFocused();
  });
}

test('revoke asks to confirm', async ({ mount, page }) => {
  const revoked: AdminUsersView = {
    ...adminUsers,
    users: [pat, ada, { ...sam, access: 'rejected' }, mira],
  };
  await stubApi(page, { '/api/admin/users': { json: adminUsers } });
  const posts: string[] = [];
  await page.route(`**/api/admin/users/${sam.id}/revoke`, (route) => {
    posts.push(route.request().method());
    return route.fulfill({ json: revoked });
  });
  await mount(<AdminUsersPage />, route);

  await row(page, sam.id).getByTestId('admin-revoke').click();
  await expect(row(page, sam.id)).toContainText('Revoke access?');
  await expect(row(page, sam.id).getByTestId('admin-revoke-cancel')).toBeFocused();
  expect(posts).toEqual([]);

  await row(page, sam.id).getByTestId('admin-revoke-cancel').click();
  await expect(row(page, sam.id).getByTestId('admin-revoke')).toBeFocused();
  expect(posts).toEqual([]);

  await row(page, sam.id).getByTestId('admin-revoke').click();
  await row(page, sam.id).getByTestId('admin-revoke-confirm').click();
  await expect(row(page, sam.id).getByTestId('admin-user-access')).toHaveText('rejected');
  await expect(row(page, sam.id).getByTestId('admin-approve')).toBeFocused();
  expect(posts).toEqual(['POST']);
});

test('every row is disabled while an action is in flight', async ({ mount, page }) => {
  await stubApi(page, { '/api/admin/users': { json: adminUsers } });
  let release: () => void = () => {};
  const gate = new Promise<void>((resolve) => (release = resolve));
  await page.route(`**/api/admin/users/${pat.id}/approve`, async (route) => {
    await gate;
    await route.fulfill({ json: adminUsers });
  });
  await mount(<AdminUsersPage />, route);

  await row(page, pat.id).getByTestId('admin-approve').click();
  await expect(row(page, sam.id).getByTestId('admin-make-admin')).toBeDisabled();
  await expect(row(page, mira.id).getByTestId('admin-approve')).toBeDisabled();
  release();
  await expect(row(page, sam.id).getByTestId('admin-make-admin')).toBeEnabled();
});

test('buttons name the person', async ({ mount, page }) => {
  await stubApi(page, { '/api/admin/users': { json: adminUsers } });
  await mount(<AdminUsersPage />, route);

  await expect(page.getByRole('button', { name: 'Approve Pat Lindqvist' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Make admin Sam Rivera' })).toBeVisible();
});

test('a pending_approval 403 is not the admins only page', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/admin/users': { status: 403, json: { error: 'pending_approval' } },
  });
  await mount(<PendingHandlerSilenced />, route);

  await expect(page.getByTestId('admin-users-error')).toHaveText('pending_approval');
  await expect(page.getByRole('heading', { name: 'Admins only' })).toHaveCount(0);
});

test('non-admin sees admins only', async ({ mount, page }) => {
  await stubApi(page, { '/api/admin/users': { status: 403, json: { error: 'Admins only' } } });
  await mount(<AdminUsersPage />, route);

  await expect(page.getByRole('heading', { name: 'Admins only' })).toBeVisible();
  await expect(page.getByTestId('admin-users-error')).toHaveText('Admins only');
  await expect(page.getByTestId('admin-user-row')).toHaveCount(0);
});

test('network failure shows the message', async ({ mount, page }) => {
  await stubApi(page, { '/api/admin/users': 'network-down' });
  await mount(<AdminUsersPage />, route);

  await expect(page.getByTestId('admin-users-error')).toHaveText("Can't reach SlideWorks");
});

test('admin users page matches its screenshot', async ({ mount, page }) => {
  await stubApi(page, { '/api/admin/users': { json: adminUsers } });
  await page.route('https://slideworks.example/**', (route) =>
    route.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
  await mount(<AdminUsersPage />, route);
  await expect(page.getByTestId('admin-user-row')).toHaveCount(4);

  await expect(page.getByTestId('admin-users-page')).toHaveScreenshot('admin-users-page.png');
});
