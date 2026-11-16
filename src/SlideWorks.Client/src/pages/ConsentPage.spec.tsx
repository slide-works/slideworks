import { test, expect } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { consent, me } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';

const REQUEST = 'req_123';
const CONSENT_PATH = `/api/oauth/consent/${REQUEST}`;

test('consent page shows client name and both buttons', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me }, [CONSENT_PATH]: { json: consent } });
  await mount(<AppRoutes />, { hooksConfig: { route: `/oauth/consent?request=${REQUEST}` } });
  await expect(page.getByTestId('consent-client-name')).toHaveText('Claude');
  await expect(page.getByTestId('consent-scope')).toContainText('decks');
  await expect(page.getByTestId('consent-allow')).toHaveText('Allow');
  await expect(page.getByTestId('consent-deny')).toHaveText('Deny');
  await expect(page.getByTestId('consent-returns-to-host')).toHaveText('127.0.0.1:33418');
  await expect(page.getByTestId('consent-gone')).toHaveCount(0);
});

test('consent page names an app scheme the answer goes back to', async ({ mount, page }) => {
  const cursor = { ...consent, clientName: 'Cursor', returnsTo: 'cursor://anysphere.cursor-mcp' };
  await stubApi(page, { '/api/me': { json: me }, [CONSENT_PATH]: { json: cursor } });
  await mount(<AppRoutes />, { hooksConfig: { route: `/oauth/consent?request=${REQUEST}` } });
  await expect(page.getByTestId('consent-client-name')).toHaveText('Cursor');
  await expect(page.getByTestId('consent-returns-to')).toContainText(
    'Your answer goes back to cursor://anysphere.cursor-mcp.',
  );
});

test('expired request shows message', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/me': { json: me },
    [CONSENT_PATH]: {
      status: 410,
      json: { error: 'This request has expired or was already answered.' },
    },
  });
  await mount(<AppRoutes />, { hooksConfig: { route: `/oauth/consent?request=${REQUEST}` } });
  await expect(page.getByTestId('consent-gone')).toContainText(
    'This request has expired or was already answered',
  );
  await expect(page.getByTestId('consent-allow')).toHaveCount(0);
});

test('allow posts approve and follows the redirect', async ({ mount, page }) => {
  const redirect = 'http://127.0.0.1:33418/callback?code=abc&state=xyz';
  let posted: unknown = null;
  await stubApi(page, { '/api/me': { json: me } });
  await page.route(`**${CONSENT_PATH}`, (route) => {
    if (route.request().method() === 'POST') {
      posted = route.request().postDataJSON();
      return route.fulfill({ json: { redirect } });
    }
    return route.fulfill({ json: consent });
  });
  await page.route('http://127.0.0.1:33418/**', (route) =>
    route.fulfill({ contentType: 'text/html', body: '<p data-testid="agent-callback">done</p>' }),
  );
  await mount(<AppRoutes />, { hooksConfig: { route: `/oauth/consent?request=${REQUEST}` } });
  await page.getByTestId('consent-allow').click();
  await expect(page).toHaveURL(redirect);
  expect(posted).toEqual({ approve: true });
});

test('no request id shows message', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me } });
  await mount(<AppRoutes />, { hooksConfig: { route: '/oauth/consent' } });
  await expect(page.getByTestId('consent-gone')).toBeVisible();
});
