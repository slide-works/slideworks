import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { me, profile } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';

const route = { hooksConfig: { route: '/profile' } };
const renamed = {
  ...profile,
  identity: {
    ...profile.identity,
    name: 'Quinn Ames',
    colour: 'teal',
    avatar: { initials: 'QA', colour: 'teal', pictureUrl: null },
  },
};

test('save is disabled until the name or colour changes and enabled after a change', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/me': { json: me }, '/api/me/profile': { json: profile } });
  await mount(<AppRoutes />, route);
  await expect(page.getByTestId('profile-name-input')).toHaveValue(profile.identity.name);
  await expect(page.getByTestId('profile-save')).toBeDisabled();

  await page.getByTestId('profile-name-input').fill('Quinn Ames');
  await expect(page.getByTestId('profile-save')).toBeEnabled();
  await page.getByTestId('profile-name-input').fill(profile.identity.name);
  await expect(page.getByTestId('profile-save')).toBeDisabled();

  await page.getByTestId('profile-colour-pink').click();
  await expect(page.getByTestId('profile-save')).toBeEnabled();
});

test('the colour options are named and cancel puts the saved values back', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/me': { json: me }, '/api/me/profile': { json: profile } });
  await mount(<AppRoutes />, route);

  await expect(page.getByRole('radio', { name: 'Teal' })).toBeVisible();
  await expect(page.getByRole('radio')).toHaveCount(profile.identity.colours.length);
  await page.getByTestId('profile-name-input').fill('Someone Else');
  await page.getByRole('radio', { name: 'Pink' }).click();
  await expect(page.getByRole('radio', { name: 'Pink' })).toHaveAttribute('aria-checked', 'true');
  await page.getByTestId('profile-cancel').click();

  await expect(page.getByTestId('profile-name-input')).toHaveValue(profile.identity.name);
  await expect(page.getByRole('radio', { name: 'Pink' })).toHaveAttribute('aria-checked', 'false');
  await expect(page.getByTestId('profile-save')).toBeDisabled();
});

test('the form saves the name and colour and shows the saved name', async ({ mount, page }) => {
  const noPicture = { ...me, avatar: { ...me.avatar, pictureUrl: null } };
  await stubApi(page, { '/api/me': { json: noPicture }, '/api/me/profile': { json: profile } });
  const bodies: unknown[] = [];
  await page.route('**/api/me/profile', (route) => {
    if (route.request().method() !== 'PUT') return route.fallback();
    bodies.push(route.request().postDataJSON());
    return route.fulfill({ json: renamed });
  });
  await mount(<AppRoutes />, route);

  await page.getByTestId('profile-name-input').fill('Quinn Ames');
  await page.getByRole('radio', { name: 'Teal' }).click();
  await page.getByTestId('profile-save').click();

  await expect(page.getByTestId('profile-name')).toHaveText('Quinn Ames');
  await expect(page.getByTestId('user-menu-avatar-initials')).toHaveText('QA');
  await expect(page.getByTestId('profile-save')).toBeDisabled();
  await expect(page.getByTestId('profile-name-input')).toBeFocused();
  expect(bodies).toEqual([{ name: 'Quinn Ames', colour: 'teal' }]);
});

test('a refused name shows the server text under the name and focuses it', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/me': { json: me }, '/api/me/profile': { json: profile } });
  await page.route('**/api/me/profile', (route) =>
    route.request().method() === 'PUT'
      ? route.fulfill({ status: 400, json: { error: 'Your name must be 1 to 60 characters.' } })
      : route.fallback(),
  );
  await mount(<AppRoutes />, route);

  await page.getByTestId('profile-name-input').fill('   ');
  await page.getByTestId('profile-save').click();

  await expect(page.getByTestId('profile-name-error')).toHaveText(
    'Your name must be 1 to 60 characters.',
  );
  await expect(page.getByTestId('profile-name-input')).toBeFocused();
  await expect(page.getByTestId('profile-name')).toHaveText(profile.identity.name);
});
