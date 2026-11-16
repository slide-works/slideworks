import { test, expect } from '@playwright/experimental-ct-react';
import type { Page, WebSocketRoute } from 'playwright/test';
import type { DeckPlayView } from '../api/types';
import { AppRoutes } from '../AppRoutes';
import { LocationProbe } from '../test-support/LocationProbe';
import { deckPlay, me } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';

const playPath = `/api/play/${deckPlay.id}`;
const canonical = `/d/${deckPlay.id}/${deckPlay.slug}`;
const signedOut = { status: 401, json: { error: 'Not signed in' } };

// SignalR's JSON hub protocol ends every message with a record separator.
const RS = '\u001e';

// Plays the deck hub (/hubs/deck) at the network boundary: answers negotiate,
// the handshake and JoinDeck, and returns a function that pushes a deckEvent to
// the client the way the server does.
async function stubDeckHub(page: Page) {
  const joined: unknown[] = [];
  let socket: WebSocketRoute | undefined;
  await page.route('**/hubs/deck/negotiate**', (route) =>
    route.fulfill({
      json: {
        negotiateVersion: 1,
        connectionId: 'conn-1',
        connectionToken: 'token-1',
        availableTransports: [{ transport: 'WebSockets', transferFormats: ['Text', 'Binary'] }],
      },
    }),
  );
  await page.routeWebSocket(/\/hubs\/deck\?/, (ws) => {
    socket = ws;
    ws.onMessage((raw) => {
      for (const frame of String(raw).split(RS).filter(Boolean)) {
        const message = JSON.parse(frame) as {
          protocol?: string;
          type?: number;
          invocationId?: string;
          target?: string;
          arguments?: unknown[];
        };
        if (message.protocol !== undefined) ws.send(`{}${RS}`);
        else if (message.type === 1 && message.target === 'JoinDeck') {
          joined.push(message.arguments?.[0]);
          ws.send(`${JSON.stringify({ type: 3, invocationId: message.invocationId })}${RS}`);
        }
      }
    });
  });
  // The component-test page is loaded before the test starts, and the
  // WebSocket route only takes effect in a page loaded after it is set.
  await page.reload();
  return {
    joined,
    push(event: unknown) {
      socket!.send(`${JSON.stringify({ type: 1, target: 'deckEvent', arguments: [event] })}${RS}`);
    },
  };
}

test('player page loads deck and renders player', async ({ mount, page }) => {
  await stubApi(page, { [playPath]: { json: deckPlay } });
  await mount(<AppRoutes />, { hooksConfig: { route: canonical } });

  await expect(page.getByTestId('player')).toBeVisible();
  for (const slide of deckPlay.slides) {
    await expect(page.getByTestId(`slide-${slide.number}`)).toHaveCount(1);
  }
  await expect(page.getByTestId('app-shell')).toHaveCount(0);
});

test('player page refetches on DeckChanged', async ({ mount, page }) => {
  const hub = await stubDeckHub(page);
  const longer: DeckPlayView = {
    ...deckPlay,
    slides: [...deckPlay.slides, { ...deckPlay.slides[1], number: 4 }],
  };
  let reads = 0;
  await page.route(`**${playPath}`, (route) => {
    reads += 1;
    return route.fulfill({ json: reads === 1 ? deckPlay : longer });
  });
  await mount(<AppRoutes />, { hooksConfig: { route: canonical } });

  await expect(page.getByTestId('slide-3')).toHaveCount(1);
  await expect(page.getByTestId('slide-4')).toHaveCount(0);
  await expect.poll(() => hub.joined).toEqual([deckPlay.id]);

  // Another deck's event is not this page's business.
  hub.push({ type: 'deckChanged', deckId: 'Zz9OtherDeck' });
  hub.push({ type: 'deckChanged', deckId: deckPlay.id });

  await expect(page.getByTestId('slide-4')).toHaveCount(1);
  expect(reads).toBe(2);
});

test('player page redirects to canonical slug keeping hash', async ({ mount, page }) => {
  await stubApi(page, { [playPath]: { json: deckPlay } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: `/d/${deckPlay.id}#3` } },
  );

  await expect(page.getByTestId('location')).toHaveText(`${canonical}#3`);
  await expect(page.getByTestId('player')).toBeVisible();
});

test('player page replaces an old slug', async ({ mount, page }) => {
  await stubApi(page, { [playPath]: { json: deckPlay } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: `/d/${deckPlay.id}/old-title#2.1` } },
  );

  await expect(page.getByTestId('location')).toHaveText(`${canonical}#2.1`);
});

test('player page 404 when signed out offers sign-in', async ({ mount, page }) => {
  await stubApi(page, {
    [playPath]: { status: 404, json: { error: 'Deck not found.' } },
    '/api/me': signedOut,
  });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: `${canonical}#2` } },
  );

  await expect(page.getByTestId('player-not-found')).toHaveText('Deck not found');
  const signIn = page.getByTestId('player-sign-in');
  await expect(signIn).toHaveAttribute(
    'href',
    `/signin?returnTo=${encodeURIComponent(`${canonical}#2`)}`,
  );
  await signIn.click();
  await expect(page.getByTestId('signin-page')).toBeVisible();
});

test('player page 404 when signed in has no sign-in', async ({ mount, page }) => {
  await stubApi(page, {
    [playPath]: { status: 404, json: { error: 'Deck not found.' } },
    '/api/me': { json: me },
  });
  let meCalls = 0;
  page.on('request', (request) => {
    if (request.url().endsWith('/api/me')) meCalls += 1;
  });
  await mount(<AppRoutes />, { hooksConfig: { route: canonical } });

  await expect(page.getByTestId('player-not-found')).toHaveText('Deck not found');
  await expect.poll(() => meCalls).toBe(1);
  await expect(page.getByTestId('player-sign-in')).toHaveCount(0);
});

test('player page api down shows message', async ({ mount, page }) => {
  await stubApi(page, { [playPath]: 'network-down' });
  await mount(<AppRoutes />, { hooksConfig: { route: canonical } });

  await expect(page.getByTestId('player-error')).toHaveText("Can't reach SlideWorks.");
});

// The baseline comes from CI's playwright job (never from a dev machine); E2
// refreshes the baselines from its report.
test('player page screenshot', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1280, height: 720 });
  await stubApi(page, { [playPath]: { json: deckPlay } });
  await mount(<AppRoutes />, { hooksConfig: { route: canonical } });
  await expect(page.getByTestId('player')).toBeVisible();
  await expect(page).toHaveScreenshot('player-page.png');
});
