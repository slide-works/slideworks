import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AppRoutes } from '../AppRoutes';
import { sampleDeck } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';

const TOKEN = 'preview-token';
const renderPath = `/api/render/${sampleDeck.id}`;
const route = (n: number, hash = `#token=${TOKEN}`) => `/render/${sampleDeck.id}/${n}${hash}`;
const stubRender = (page: Page, read: { status?: number; json?: unknown } = { json: sampleDeck }) =>
  stubApi(page, { [`${renderPath}/session`]: { json: {} }, [renderPath]: read });

// Every request the page makes to the API or the realtime hub, with its token header.
function recordRequests(page: Page) {
  const seen: { url: string; method: string; token: string | undefined }[] = [];
  page.on('request', (request) => {
    const path = new URL(request.url()).pathname;
    if (path.startsWith('/api/') || path.startsWith('/hubs/')) {
      seen.push({
        url: path,
        method: request.method(),
        token: request.headers()['x-render-token'],
      });
    }
  });
  return seen;
}

test('render page shows one slide at 1920x1080 with every step revealed and sets data-ready', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 1920, height: 1080 });
  const bullets = sampleDeck.slides.find((s) => s.steps > 0)!;
  await stubRender(page);
  const requests = recordRequests(page);

  await mount(<AppRoutes />, { hooksConfig: { route: route(bullets.number) } });

  const root = page.getByTestId('render-slide');
  await expect(root).toHaveAttribute('data-ready', 'true');
  await expect(root).not.toHaveAttribute('data-error', /.*/);
  expect(await root.boundingBox()).toEqual({ x: 0, y: 0, width: 1920, height: 1080 });
  await expect(page.getByTestId(`slide-${bullets.number}`)).toHaveCount(1);
  for (const other of sampleDeck.slides.filter((s) => s.number !== bullets.number)) {
    await expect(page.getByTestId(`slide-${other.number}`)).toHaveCount(0);
  }
  const reveals = root.locator('.sw-reveal');
  await expect(reveals).toHaveCount(bullets.steps);
  await expect(root.locator('.sw-reveal[data-revealed="false"]')).toHaveCount(0);
  await expect(page.getByTestId('player')).toHaveCount(0);
  await expect(page.getByTestId('app-shell')).toHaveCount(0);
  expect(requests.map((r) => `${r.method} ${r.url}`)).toEqual([
    `POST ${renderPath}/session`,
    `GET ${renderPath}`,
  ]);
});

test('render page sends the token only as X-Render-Token and opens no hub connection', async ({
  mount,
  page,
}) => {
  await stubRender(page);
  const requests = recordRequests(page);

  await mount(<AppRoutes />, { hooksConfig: { route: route(1) } });

  await expect(page.getByTestId('render-slide')).toHaveAttribute('data-ready', 'true');
  const api = requests.filter((r) => !r.url.startsWith('/api/images/'));
  expect(api.length).toBeGreaterThan(0);
  for (const request of api) expect(request.token, request.url).toBe(TOKEN);
  expect(requests.filter((r) => r.url.startsWith('/hubs/'))).toEqual([]);
  expect(requests.some((r) => r.url.includes(TOKEN))).toBe(false);
});

test('render page removes the token from the address bar', async ({ mount, page }) => {
  await stubRender(page);
  await page.evaluate(
    (hash) => window.history.replaceState(window.history.state, '', hash),
    `#token=${TOKEN}`,
  );

  await mount(<AppRoutes />, { hooksConfig: { route: route(1) } });

  await expect(page.getByTestId('render-slide')).toHaveAttribute('data-ready', 'true');
  expect(await page.evaluate(() => window.location.href)).not.toContain(TOKEN);
});

test('render page shows the API error when the token has expired', async ({ mount, page }) => {
  await stubRender(page, { status: 404, json: { error: 'Deck not found' } });

  await mount(<AppRoutes />, { hooksConfig: { route: route(1) } });

  const root = page.getByTestId('render-slide');
  await expect(root).toHaveAttribute('data-error', 'Deck not found');
  await expect(root).not.toHaveAttribute('data-ready', /.*/);
  // The screenshot shows why: the error is drawn as text, and nothing else is.
  await expect(page.getByTestId('render-error')).toBeVisible();
  await expect(page.getByTestId('render-error')).toHaveText('Deck not found');
  await expect(root.locator(':scope > *')).toHaveCount(1);
  await expect(root.locator('[data-testid^="slide"]')).toHaveCount(0);
});

test('render page says which slide is unknown', async ({ mount, page }) => {
  await stubRender(page);

  await mount(<AppRoutes />, { hooksConfig: { route: route(40) } });

  const root = page.getByTestId('render-slide');
  await expect(root).toHaveAttribute('data-error', 'No slide 40');
  await expect(root).not.toHaveAttribute('data-ready', /.*/);
});

test('render page without a token says so and calls nothing', async ({ mount, page }) => {
  const requests = recordRequests(page);

  await mount(<AppRoutes />, { hooksConfig: { route: route(1, '') } });

  await expect(page.getByTestId('render-slide')).toHaveAttribute(
    'data-error',
    'Render token missing',
  );
  expect(requests).toEqual([]);
});

const FONT_FILE = 'node_modules/@fontsource/inter/files/inter-latin-400-normal.woff2';
const fontUrl = '/api/templates/tpl_h/assets/head';
const fontDeck = {
  ...sampleDeck,
  look: {
    ...sampleDeck.look,
    fonts: [{ family: 'Harbour Head', role: 'head', url: fontUrl, weight: 800, style: 'normal' }],
  },
};

test('render page signals ready after fonts load', async ({ mount, page }) => {
  await stubRender(page, { json: fontDeck });
  let release: () => void = () => undefined;
  const held = new Promise<void>((resolve) => (release = resolve));
  await page.route(`**${fontUrl}`, async (route) => {
    await held;
    await route.fulfill({ path: FONT_FILE, contentType: 'font/woff2' });
  });

  await mount(<AppRoutes />, { hooksConfig: { route: route(1) } });

  const root = page.getByTestId('render-slide');
  await expect(page.getByTestId('slide-1')).toBeVisible();
  await expect.poll(() => page.evaluate(() => document.fonts.size)).toBeGreaterThan(0);
  await expect(root).not.toHaveAttribute('data-ready', /.*/);
  release();
  await expect(root).toHaveAttribute('data-ready', 'true');
});

test('render page signals ready when a font fails', async ({ mount, page }) => {
  await stubRender(page, { json: fontDeck });
  await page.route(`**${fontUrl}`, (r) => r.fulfill({ status: 404, json: { error: 'gone' } }));

  await mount(<AppRoutes />, { hooksConfig: { route: route(1) } });

  const root = page.getByTestId('render-slide');
  await expect(root).toHaveAttribute('data-ready', 'true');
  await expect(root).not.toHaveAttribute('data-error', /.*/);
});
