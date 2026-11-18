import { test, expect } from '@playwright/experimental-ct-react';
import type { Page, Request } from 'playwright/test';
import type { CardView } from '../api/types';
import { AppRoutes } from '../AppRoutes';
import { card as sampleCard } from '../test-support/fixtures/screens';
import { SAMPLE_IMAGE_ID } from '../test-support/fixtures/layoutSlots';
import { stubApi } from '../test-support/stubApi';

const DECK = 'deck123';
const TOKEN = 'card-token';
const cardPath = `/api/render/${DECK}/card`;
// The card sample is a built-in deck's, with no title art. The page draws its waves from the template's title art, so
// the cards mounted here name it themselves.
const card: CardView = {
  ...sampleCard,
  look: { ...sampleCard.look, template: { ...sampleCard.look.template, titleArt: 'waves' } },
};
const linkCard: CardView = {
  ...card,
  look: { ...card.look, logo: null },
};
const withLogo: CardView = {
  ...card,
  look: { ...card.look, logo: { url: `/api/images/${SAMPLE_IMAGE_ID}` } },
};

// Every /api request and hub connection the page makes, so a test can prove
// none goes out without the render token (no signed-in fetch slips in).
function recordRequests(page: Page) {
  const requests: Request[] = [];
  page.on('request', (request) => {
    const path = new URL(request.url()).pathname;
    if (path.startsWith('/api/') || path.startsWith('/hubs/')) requests.push(request);
  });
  page.on('websocket', (socket) => {
    throw new Error(`unexpected socket ${socket.url()}`);
  });
  return requests;
}

async function stubCard(page: Page, view: CardView) {
  await stubApi(page, { [cardPath]: { json: view } });
}

test('card page for a link deck shows its title, subtitle, byline and swatch', async ({
  mount,
  page,
}) => {
  await stubCard(page, linkCard);
  const requests = recordRequests(page);
  await mount(<AppRoutes />, { hooksConfig: { route: `/card/${DECK}#token=${TOKEN}` } });

  const shareCard = page.getByTestId('share-card');
  await expect(shareCard).toHaveAttribute('data-ready', 'true');
  await expect(page.getByTestId('share-card-title')).toHaveText(card.title!);
  await expect(page.getByTestId('share-card-subtitle')).toHaveText(card.subtitle!);
  await expect(page.getByTestId('share-card-byline')).toHaveText(
    `${card.slideCount} slides · ${card.author}`,
  );
  await expect(page.getByTestId('share-card-swatch')).toBeVisible();
  await expect(shareCard).toHaveAttribute('data-generic', 'false');
  await expect(shareCard).not.toHaveAttribute('data-error');

  expect(
    requests.map((request) => `${request.method()} ${new URL(request.url()).pathname}`),
  ).toEqual([`GET ${cardPath}`]);
  expect(await requests[0].allHeaders()).toMatchObject({ 'x-render-token': TOKEN });
  expect((await requests[0].allHeaders()).cookie).toBeUndefined();
});

test('card page waits for the logo before it is ready', async ({ mount, page }) => {
  await stubCard(page, withLogo);
  const requests = recordRequests(page);
  await mount(<AppRoutes />, { hooksConfig: { route: `/card/${DECK}#token=${TOKEN}` } });

  await expect(page.getByTestId('share-card')).toHaveAttribute('data-ready', 'true');
  await expect(page.getByTestId('share-card-swatch')).toHaveCount(0);
  // Every request, the logo's included, carries the token in the header.
  expect(requests.map((request) => new URL(request.url()).pathname)).toEqual([
    cardPath,
    `/api/images/${SAMPLE_IMAGE_ID}`,
  ]);
  for (const request of requests) {
    expect(await request.allHeaders()).toMatchObject({ 'x-render-token': TOKEN });
  }
});

test('card page sends the token header and no cookie on every /api request', async ({
  mount,
  page,
}) => {
  // A cookie the page could send: credentials 'omit' must keep it off every request.
  await page
    .context()
    .addCookies([
      { name: 'sw_session', value: 'signed-in', url: page.url() || 'http://localhost' },
    ]);
  await stubCard(page, withLogo);
  const requests = recordRequests(page);
  await mount(<AppRoutes />, { hooksConfig: { route: `/card/${DECK}#token=${TOKEN}` } });

  await expect(page.getByTestId('share-card')).toHaveAttribute('data-ready', 'true');
  expect(requests).toHaveLength(2);
  for (const request of requests) {
    // allHeaders(), because headers() hides the Cookie header.
    const headers = await request.allHeaders();
    expect(headers['x-render-token']).toBe(TOKEN);
    expect(headers.cookie).toBeUndefined();
  }
});

test('card page removes the token from the address bar', async ({ mount, page }) => {
  await stubCard(page, linkCard);
  await mount(<AppRoutes />, { hooksConfig: { route: `/card/${DECK}#token=${TOKEN}` } });

  await expect(page.getByTestId('share-card')).toHaveAttribute('data-ready', 'true');
  expect(await page.evaluate(() => window.location.href)).not.toContain(TOKEN);
});

test('card page for a logo that fails to load sets data-error', async ({ mount, page }) => {
  await stubCard(page, { ...card, look: { ...card.look, logo: { url: '/api/images/missing' } } });
  await page.route('**/api/images/missing', (route) => route.fulfill({ status: 404 }));
  await mount(<AppRoutes />, { hooksConfig: { route: `/card/${DECK}#token=${TOKEN}` } });

  const shareCard = page.getByTestId('share-card');
  // The engine adds 'Rendering failed: ', giving L9's 'Rendering failed: image <id>'.
  await expect(shareCard).toHaveAttribute('data-error', 'image missing');
  await expect(shareCard).not.toHaveAttribute('data-ready');
});

test('card page without a token sets data-error and makes no request', async ({ mount, page }) => {
  const requests = recordRequests(page);
  await mount(<AppRoutes />, { hooksConfig: { route: `/card/${DECK}` } });

  const shareCard = page.getByTestId('share-card');
  await expect(shareCard).toHaveAttribute('data-error', 'Render token missing');
  await expect(shareCard).not.toHaveAttribute('data-ready');
  expect(requests).toEqual([]);
});

test('card page shows the server error when the card is not found', async ({ mount, page }) => {
  await stubApi(page, { [cardPath]: { status: 404, json: { error: 'Deck not found' } } });
  await mount(<AppRoutes />, { hooksConfig: { route: `/card/${DECK}#token=${TOKEN}` } });

  const shareCard = page.getByTestId('share-card');
  await expect(shareCard).toHaveAttribute('data-error', 'Card request failed: Deck not found');
  await expect(page.getByTestId('share-card-title')).toHaveCount(0);
});

test('card page for a deck the server makes generic names nothing about it', async ({
  mount,
  page,
}) => {
  await stubCard(page, {
    ...card,
    generic: true,
    title: null,
    subtitle: null,
    author: null,
    slideCount: 0,
  });
  await mount(<AppRoutes />, { hooksConfig: { route: `/card/${DECK}#token=${TOKEN}` } });

  const shareCard = page.getByTestId('share-card');
  await expect(shareCard).toHaveAttribute('data-ready', 'true');
  await expect(shareCard).toHaveAttribute('data-generic', 'true');
  await expect(shareCard).toHaveText('SlideWorks');
});

test('generic card says only SlideWorks and makes no request', async ({ mount, page }) => {
  const requests = recordRequests(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/card/generic' } });

  const shareCard = page.getByTestId('share-card');
  await expect(shareCard).toHaveAttribute('data-ready', 'true');
  await expect(shareCard).toHaveAttribute('data-generic', 'true');
  await expect(shareCard).toHaveText('SlideWorks');
  expect(requests).toEqual([]);
});

// Baselines come from CI's playwright job (ct-new-baselines), never this host.
test('link deck card screenshot', async ({ mount, page }) => {
  await stubCard(page, linkCard);
  await mount(<AppRoutes />, { hooksConfig: { route: `/card/${DECK}#token=${TOKEN}` } });

  const shareCard = page.getByTestId('share-card');
  await expect(shareCard).toHaveAttribute('data-ready', 'true');
  await expect(shareCard).toHaveScreenshot('card-link-deck.png');
});

test('generic card screenshot', async ({ mount, page }) => {
  await mount(<AppRoutes />, { hooksConfig: { route: '/card/generic' } });

  const shareCard = page.getByTestId('share-card');
  await expect(shareCard).toHaveAttribute('data-ready', 'true');
  await expect(shareCard).toHaveScreenshot('card-generic.png');
});
