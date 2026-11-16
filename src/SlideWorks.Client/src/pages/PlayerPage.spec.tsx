import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { DeckPlayView } from '../api/types';
import { AppRoutes } from '../AppRoutes';
import { GoTo } from '../test-support/GoTo';
import { LocationProbe } from '../test-support/LocationProbe';
import { deckPlay, me } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';
import { stubDeckHub } from '../test-support/stubDeckHub';

const playPath = `/api/play/${deckPlay.id}`;
const canonical = `/d/${deckPlay.id}/${deckPlay.slug}`;
const signedOut = { status: 401, json: { error: 'Not signed in' } };

// The Player keeps its position in the window's hash, which MemoryRouter never
// sees; a deep link sets both, as a real address bar does.
async function setWindowHash(page: Page, hash: string) {
  await page.evaluate((next) => window.history.replaceState(window.history.state, '', next), hash);
}

const windowHash = (page: Page) => page.evaluate(() => window.location.hash);

// A response held until release(), to land answers out of order.
function gate() {
  let release!: () => void;
  const opened = new Promise<void>((resolve) => {
    release = resolve;
  });
  return { opened, release };
}

// Lets the page settle after a held response is released: the fetch resolves,
// its JSON is parsed and React renders.
const settle = (page: Page) => page.evaluate(() => new Promise((done) => setTimeout(done, 200)));

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

  // The presenter is on slide 2 when the deck changes.
  await page.evaluate(() => {
    window.location.hash = '#2';
  });
  await expect.poll(() => windowHash(page)).toBe('#2');
  const player = await page.getByTestId('player').elementHandle();

  // Another deck's event is not this page's business.
  hub.push({ type: 'deckChanged', deckId: 'Zz9OtherDeck' });
  hub.push({ type: 'deckChanged', deckId: deckPlay.id });

  await expect(page.getByTestId('slide-4')).toHaveCount(1);
  expect(reads).toBe(2);
  // The same Player took the new deck: it was not remounted, and stayed on slide 2.
  expect(await player!.evaluate((element) => element.isConnected)).toBe(true);
  expect(await windowHash(page)).toBe('#2');
});

test('player page keeps the newest deck when an older re-read answers last', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page);
  const longer: DeckPlayView = {
    ...deckPlay,
    slides: [...deckPlay.slides, { ...deckPlay.slides[1], number: 4 }],
  };
  const held = gate();
  let reads = 0;
  await page.route(`**${playPath}`, async (route) => {
    reads += 1;
    if (reads === 2) {
      // The older re-read: the deck before slide 4 was added, answering last.
      await held.opened;
      return route.fulfill({ json: deckPlay });
    }
    return route.fulfill({ json: reads === 1 ? deckPlay : longer });
  });
  await mount(<AppRoutes />, { hooksConfig: { route: canonical } });
  await expect(page.getByTestId('slide-3')).toHaveCount(1);
  await expect.poll(() => hub.joined).toEqual([deckPlay.id]);

  hub.push({ type: 'deckChanged', deckId: deckPlay.id });
  await expect.poll(() => reads).toBe(2);
  hub.push({ type: 'deckChanged', deckId: deckPlay.id });
  await expect(page.getByTestId('slide-4')).toHaveCount(1);

  held.release();
  await expect.poll(() => reads).toBe(3);
  await settle(page);

  await expect(page.getByTestId('slide-4')).toHaveCount(1);
});

test('player page ignores a re-read for the deck it moved away from', async ({ mount, page }) => {
  const hub = await stubDeckHub(page);
  const other: DeckPlayView = { ...deckPlay, id: 'Bb3OtherDeck', slug: 'other', title: 'Other' };
  const held = gate();
  let released = false;
  let reads = 0;
  await page.route(`**${playPath}`, async (route) => {
    reads += 1;
    if (reads === 2) {
      await held.opened;
      await route.fulfill({ json: deckPlay });
      released = true;
      return;
    }
    return route.fulfill({ json: deckPlay });
  });
  await stubApi(page, { [`/api/play/${other.id}`]: { json: other } });
  await mount(
    <GoTo>
      <AppRoutes />
    </GoTo>,
    { hooksConfig: { route: canonical } },
  );
  await expect(page.getByTestId('player')).toHaveAttribute('aria-label', deckPlay.title);
  await expect.poll(() => hub.joined).toEqual([deckPlay.id]);

  // The deck changes, and while its re-read is still out, the viewer opens another deck.
  hub.push({ type: 'deckChanged', deckId: deckPlay.id });
  await expect.poll(() => reads).toBe(2);
  await page.getByTestId('goto-path').fill(`/d/${other.id}/${other.slug}`);
  await page.getByTestId('goto').click();
  await expect(page.getByTestId('player')).toHaveAttribute('aria-label', 'Other');

  held.release();
  await expect.poll(() => released).toBe(true);
  await settle(page);

  await expect(page.getByTestId('player-page')).toHaveAttribute('data-deck-id', other.id);
  await expect(page.getByTestId('player')).toHaveAttribute('aria-label', 'Other');
});

test('player page 404 mid-presentation offers sign-in back to the current slide', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page);
  let reads = 0;
  await page.route(`**${playPath}`, (route) => {
    reads += 1;
    return reads === 1
      ? route.fulfill({ json: deckPlay })
      : route.fulfill({ status: 404, json: { error: 'Deck not found.' } });
  });
  await stubApi(page, { '/api/me': signedOut });
  await mount(<AppRoutes />, { hooksConfig: { route: canonical } });
  await expect(page.getByTestId('player')).toBeVisible();
  await expect.poll(() => hub.joined).toEqual([deckPlay.id]);

  // The viewer has moved on to slide 2 when the deck is made private.
  await page.evaluate(() => {
    window.location.hash = '#2';
  });
  await expect.poll(() => windowHash(page)).toBe('#2');
  hub.push({ type: 'deckChanged', deckId: deckPlay.id });

  await expect(page.getByTestId('player-sign-in')).toHaveAttribute(
    'href',
    `/signin?returnTo=${encodeURIComponent(`${canonical}#2`)}`,
  );
});

test('player page redirects to canonical slug keeping hash', async ({ mount, page }) => {
  await stubApi(page, { [playPath]: { json: deckPlay } });
  await setWindowHash(page, '#3');
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    { hooksConfig: { route: `/d/${deckPlay.id}#3` } },
  );

  await expect(page.getByTestId('location')).toHaveText(`${canonical}#3`);
  await expect(page.getByTestId('player')).toBeVisible();
  expect(await windowHash(page)).toBe('#3');
});

test('player page replaces an old slug', async ({ mount, page }) => {
  await stubApi(page, { [playPath]: { json: deckPlay } });
  await setWindowHash(page, '#2.1');
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
  await setWindowHash(page, '#2');
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
