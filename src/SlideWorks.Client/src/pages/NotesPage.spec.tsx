import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { seedApiToken } from '../test-support/seedApiToken';
import { stubNotes } from '../test-support/stubNotes';

const NOTES = [
  { id: 2, text: 'Second thought', created: '2026-01-02 03:04 UTC' },
  { id: 1, text: 'First thought', created: '2026-01-01 09:30 UTC' },
];

test.beforeEach(async ({ page }) => {
  await seedApiToken(page);
  await stubNotes(page, NOTES);
});

test('lists the notes the API returns, newest first', async ({ mount, page }) => {
  await mount(<AppRoutes />, { hooksConfig: { route: '/' } });
  await expect(page.getByRole('listitem')).toHaveText([/Second thought/, /First thought/]);
});

// The header is masked: it carries the project name, which is different in every
// project made from the template, and these baselines have to hold for all of them.
test('screenshot: notes page', async ({ mount, page }) => {
  await mount(<AppRoutes />, { hooksConfig: { route: '/' } });
  await expect(page.getByText('First thought')).toBeVisible();
  await expect(page).toHaveScreenshot({ mask: [page.getByRole('banner')] });
});

test.describe('mobile', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test('screenshot: notes page (mobile)', async ({ mount, page }) => {
    await mount(<AppRoutes />, { hooksConfig: { route: '/' } });
    await expect(page.getByText('First thought')).toBeVisible();
    await expect(page).toHaveScreenshot({ mask: [page.getByRole('banner')] });
  });
});
