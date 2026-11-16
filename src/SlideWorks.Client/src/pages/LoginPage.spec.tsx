import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';

test('renders the token form', async ({ mount, page }) => {
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await expect(page.getByLabel('API token')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Enter' })).toBeVisible();
});

test('redirects here when no token is stored', async ({ mount, page }) => {
  await mount(<AppRoutes />, { hooksConfig: { route: '/' } });
  await expect(page.getByLabel('API token')).toBeVisible();
});

// The heading is masked: it is the project name, different in every project made
// from the template.
test('screenshot: login page', async ({ mount, page }) => {
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await expect(page.getByLabel('API token')).toBeVisible();
  await expect(page).toHaveScreenshot({ mask: [page.getByRole('heading', { level: 1 })] });
});
