import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AppRoutes } from '../../AppRoutes';
import type { ProfileView } from '../../api/profile';
import { me, profile } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';

const route = { hooksConfig: { route: '/profile' } };
const png = { name: 'me.png', mimeType: 'image/png', buffer: Buffer.from([1, 2, 3]) };

const withoutPhoto: ProfileView = {
  ...profile,
  identity: {
    ...profile.identity,
    hasPhoto: false,
    avatar: { ...profile.identity.avatar, pictureUrl: null },
  },
};
const withPhoto: ProfileView = {
  ...profile,
  identity: {
    ...profile.identity,
    hasPhoto: true,
    avatar: { ...profile.identity.avatar, pictureUrl: '/api/people/u1/photo/p1' },
  },
};

async function open(page: Page, start: ProfileView) {
  await stubApi(page, { '/api/me': { json: me }, '/api/me/profile': { json: start } });
  await page.route('**/api/people/**', (route) =>
    route.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
}

test('choosing a file shows the new picture and the uploaded line without Save', async ({
  mount,
  page,
}) => {
  await open(page, withoutPhoto);
  const posts: { type: string | undefined; body: Buffer | null }[] = [];
  await page.route('**/api/me/photo', (r) => {
    posts.push({ type: r.request().headers()['content-type'], body: r.request().postDataBuffer() });
    return r.fulfill({ json: withPhoto });
  });
  await mount(<AppRoutes />, route);
  await expect(page.getByTestId('profile-photo-source')).toHaveText('Picture: Initials');

  await page.getByTestId('profile-photo-input').setInputFiles(png);

  await expect(page.getByTestId('profile-photo-source')).toHaveText('Picture: Your photo');
  await expect(page.getByTestId('profile-photo-avatar-picture')).toHaveAttribute(
    'src',
    '/api/people/u1/photo/p1',
  );
  await expect(page.getByRole('button', { name: 'Replace photo' })).toBeFocused();
  expect(posts).toEqual([{ type: 'image/png', body: Buffer.from([1, 2, 3]) }]);
});

test('a refusal shows the server text under the photo and changes nothing', async ({
  mount,
  page,
}) => {
  await open(page, withoutPhoto);
  await page.route('**/api/me/photo', (r) =>
    r.fulfill({ status: 400, json: { error: 'That looks like a damaged image' } }),
  );
  await mount(<AppRoutes />, route);

  await page.getByTestId('profile-photo-input').setInputFiles(png);

  await expect(page.getByTestId('profile-photo-error')).toHaveText('That looks like a damaged image');
  await expect(page.getByTestId('profile-photo-source')).toHaveText('Picture: Initials');
  await expect(page.getByRole('button', { name: 'Remove photo' })).toHaveCount(0);
});

test('Remove photo shows only while a photo is uploaded and falls back on removal', async ({
  mount,
  page,
}) => {
  await open(page, withPhoto);
  await page.route('**/api/me/photo', (r) =>
    r.request().method() === 'DELETE' ? r.fulfill({ json: withoutPhoto }) : r.fallback(),
  );
  await mount(<AppRoutes />, route);
  await expect(page.getByRole('button', { name: 'Remove photo' })).toBeVisible();

  await page.getByRole('button', { name: 'Remove photo' }).click();

  await expect(page.getByRole('button', { name: 'Remove photo' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Upload photo' })).toBeFocused();
  await expect(page.getByTestId('profile-photo-source')).toHaveText('Picture: Initials');
});

test('without a photo there is no Remove photo button', async ({ mount, page }) => {
  await open(page, withoutPhoto);
  await mount(<AppRoutes />, route);

  await expect(page.getByRole('button', { name: 'Upload photo' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Remove photo' })).toHaveCount(0);
});
