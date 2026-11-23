import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { DeckPlayView } from '../api/types';
import { AppRoutes } from '../AppRoutes';
import { GoTo } from '../test-support/GoTo';
import { LocationProbe } from '../test-support/LocationProbe';
import { deckPlay, me } from '../test-support/fixtures/screens';
import { longerDeckPlay } from '../test-support/fixtures/longerDecks';
import { guestView } from '../test-support/fixtures/guestStates';
import { stubApi } from '../test-support/stubApi';
import { gate, settle } from '../test-support/gate';
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
  let reads = 0;
  await page.route(`**${playPath}`, (route) => {
    reads += 1;
    return route.fulfill({ json: reads <= 2 ? deckPlay : longerDeckPlay });
  });
  await mount(<AppRoutes />, { hooksConfig: { route: canonical } });

  await expect(page.getByTestId('slide-3')).toHaveCount(1);
  await expect(page.getByTestId('slide-4')).toHaveCount(0);
  await expect.poll(() => hub.joined).toEqual([deckPlay.id]);
  // The join's own re-read comes first.
  await expect.poll(() => reads).toBe(2);

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
  expect(reads).toBe(3);
  // The same Player took the new deck: it was not remounted, and stayed on slide 2.
  expect(await player!.evaluate((element) => element.isConnected)).toBe(true);
  expect(await windowHash(page)).toBe('#2');
});

test('player page keeps the newest deck when an older re-read answers last', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page);
  const held = gate();
  let reads = 0;
  await page.route(`**${playPath}`, async (route) => {
    reads += 1;
    if (reads === 3) {
      // The older re-read: the deck before slide 4 was added, answering last.
      await held.opened;
      return route.fulfill({ json: deckPlay });
    }
    return route.fulfill({ json: reads <= 2 ? deckPlay : longerDeckPlay });
  });
  await mount(<AppRoutes />, { hooksConfig: { route: canonical } });
  await expect(page.getByTestId('slide-3')).toHaveCount(1);
  await expect.poll(() => hub.joined).toEqual([deckPlay.id]);
  // The join's own re-read comes first.
  await expect.poll(() => reads).toBe(2);

  hub.push({ type: 'deckChanged', deckId: deckPlay.id });
  await expect.poll(() => reads).toBe(3);
  hub.push({ type: 'deckChanged', deckId: deckPlay.id });
  await expect(page.getByTestId('slide-4')).toHaveCount(1);

  held.release();
  await expect.poll(() => reads).toBe(4);
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
    if (reads === 3) {
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
  // The join's own re-read comes first.
  await expect.poll(() => reads).toBe(2);

  // The deck changes, and while its re-read is still out, the viewer opens another deck.
  hub.push({ type: 'deckChanged', deckId: deckPlay.id });
  await expect.poll(() => reads).toBe(3);
  await page.getByTestId('goto-path').fill(`/d/${other.id}/${other.slug}`);
  await page.getByTestId('goto').click();
  await expect(page.getByTestId('player')).toHaveAttribute('aria-label', 'Other');

  held.release();
  await expect.poll(() => released).toBe(true);
  await settle(page);

  await expect(page.getByTestId('player-page')).toHaveAttribute('data-deck-id', other.id);
  await expect(page.getByTestId('player')).toHaveAttribute('aria-label', 'Other');
});

// A change that lands after the first read but before JoinDeck completes sends
// no event to this page, so the page re-reads once it has joined.
test('player page re-reads once it has joined, showing a change made before the join', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, { holdJoins: true });
  let reads = 0;
  await page.route(`**${playPath}`, (route) => {
    reads += 1;
    return route.fulfill({ json: reads === 1 ? deckPlay : longerDeckPlay });
  });
  await mount(<AppRoutes />, { hooksConfig: { route: canonical } });
  await expect(page.getByTestId('slide-3')).toHaveCount(1);
  await expect.poll(() => hub.joined).toEqual([deckPlay.id]);

  // The viewer is on slide 2 when an agent adds slide 4, while the join is still out.
  await page.evaluate(() => {
    window.location.hash = '#2';
  });
  await expect.poll(() => windowHash(page)).toBe('#2');
  const player = await page.getByTestId('player').elementHandle();
  await settle(page);
  expect(reads).toBe(1);
  hub.releaseJoins();

  await expect(page.getByTestId('slide-4')).toHaveCount(1);
  expect(reads).toBe(2);
  // The same Player took the new deck, still on slide 2.
  expect(await player!.evaluate((element) => element.isConnected)).toBe(true);
  expect(await windowHash(page)).toBe('#2');
});

test('player page waits for its first read before the join re-reads', async ({ mount, page }) => {
  const hub = await stubDeckHub(page);
  const first = gate();
  let reads = 0;
  await page.route(`**${playPath}`, async (route) => {
    reads += 1;
    if (reads === 1) {
      await first.opened;
      return route.fulfill({ json: deckPlay });
    }
    return route.fulfill({ json: longerDeckPlay });
  });
  await mount(<AppRoutes />, { hooksConfig: { route: canonical } });
  await expect.poll(() => hub.joined).toEqual([deckPlay.id]);

  // Joined while the first read is still out: nothing is thrown away for a re-read.
  await settle(page);
  expect(reads).toBe(1);
  first.release();

  await expect(page.getByTestId('slide-4')).toHaveCount(1);
  expect(reads).toBe(2);
});

// A join that lands while the first read is out must not re-read once the page
// has gone. Every read answers 404 here, so a stray re-read would also ask
// /api/me whether to offer sign-in.
test('player page sends no re-read after it unmounts with a join waiting on its first read', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page);
  const first = gate();
  let answered = false;
  let reads = 0;
  let meReads = 0;
  await page.route('**/api/me', (route) => {
    meReads += 1;
    return route.fulfill(signedOut);
  });
  await page.route(`**${playPath}`, async (route) => {
    reads += 1;
    if (reads === 1) {
      await first.opened;
      await route.fulfill({ status: 404, json: { error: 'Deck not found' } });
      answered = true;
      return;
    }
    return route.fulfill({ status: 404, json: { error: 'Deck not found' } });
  });
  const component = await mount(<AppRoutes />, { hooksConfig: { route: canonical } });
  await expect.poll(() => hub.joined).toEqual([deckPlay.id]);
  await settle(page);
  expect(reads).toBe(1);

  await component.unmount();
  first.release();
  await expect.poll(() => answered).toBe(true);
  await settle(page);

  expect(reads).toBe(1);
  expect(meReads).toBe(0);
});

test('player page re-reads when a reconnect joins again', async ({ mount, page }) => {
  const hub = await stubDeckHub(page, { audience: { state: guestView('live').state } });
  // The owner: GET live answers 200, which is what draws the chip.
  await stubApi(page, {
    [`/api/decks/${deckPlay.id}/live`]: {
      json: {
        sessionId: null,
        code: deckPlay.join.code,
        codeLabel: deckPlay.join.codeLabel,
        joinUrl: deckPlay.join.joinUrl,
        hostLabel: deckPlay.join.hostLabel,
        live: false,
        joinedCount: 0,
        joinBar: false,
      },
    },
  });
  let reads = 0;
  await page.route(`**${playPath}`, (route) => {
    reads += 1;
    return route.fulfill({ json: reads <= 2 ? deckPlay : longerDeckPlay });
  });
  await mount(<AppRoutes />, { hooksConfig: { route: canonical } });
  await expect.poll(() => hub.joined).toEqual([deckPlay.id]);
  await expect.poll(() => reads).toBe(2);
  await expect(page.getByTestId('slide-3')).toHaveCount(1);
  const player = await page.getByTestId('player').elementHandle();

  // The connection drops, slide 4 is added while it is down, and SignalR reconnects.
  hub.drop();

  await expect.poll(() => hub.joined).toEqual([deckPlay.id, deckPlay.id]);
  await expect(page.getByTestId('slide-4')).toHaveCount(1);
  expect(reads).toBe(3);
  expect(await player!.evaluate((element) => element.isConnected)).toBe(true);
});

test('player page 404 mid-presentation offers sign-in back to the current slide', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, { audience: { state: guestView('live').state } });
  // A signed-out viewer of a link deck: the play view says they are not the owner, so GET live is never called, no chip is
  // drawn and no redirect happens.
  let liveReads = 0;
  await page.route(`**/api/decks/${deckPlay.id}/live`, (route) => {
    liveReads += 1;
    return route.fulfill({ status: 403, json: { error: "Only the deck's owner can go live" } });
  });
  let reads = 0;
  await page.route(`**${playPath}`, (route) => {
    reads += 1;
    return reads <= 2
      ? route.fulfill({ json: { ...deckPlay, isOwner: false } })
      : route.fulfill({ status: 404, json: { error: 'Deck not found.' } });
  });
  await stubApi(page, { '/api/me': signedOut });
  await mount(<AppRoutes />, { hooksConfig: { route: canonical } });
  await expect(page.getByTestId('player')).toBeVisible();
  await expect.poll(() => hub.joined).toEqual([deckPlay.id]);
  // The join's own re-read comes first.
  await expect.poll(() => reads).toBe(2);
  await expect(page.getByTestId('live-chip')).toHaveCount(0);

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
  await expect(page.getByTestId('live-chip')).toHaveCount(0);
  expect(liveReads).toBe(0);
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

test('player page mounts the live chip and re-reads the deck when liveChanged arrives', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page, { audience: { state: guestView('live').state } });
  // The owner: GET live answers 200, which is what draws the chip.
  await stubApi(page, {
    [`/api/decks/${deckPlay.id}/live`]: {
      json: {
        sessionId: null,
        code: deckPlay.join.code,
        codeLabel: deckPlay.join.codeLabel,
        joinUrl: deckPlay.join.joinUrl,
        hostLabel: deckPlay.join.hostLabel,
        live: false,
        joinedCount: 0,
        joinBar: false,
      },
    },
  });
  let reads = 0;
  await page.route(`**${playPath}`, (route) => {
    reads += 1;
    return route.fulfill({
      json: reads <= 2 ? deckPlay : { ...deckPlay, join: { ...deckPlay.join, live: true } },
    });
  });
  await mount(<AppRoutes />, { hooksConfig: { route: canonical } });
  await expect(page.getByTestId('live-chip')).toBeVisible();
  await expect.poll(() => hub.joined).toEqual([deckPlay.id]);
  await expect.poll(() => reads).toBe(2);

  hub.push({ type: 'liveChanged', deckId: deckPlay.id });

  await expect.poll(() => reads).toBe(3);
});

// A GET live that would fail with a 500, to prove who calls it.
async function stubBrokenLive(page: Page, view: DeckPlayView) {
  let liveReads = 0;
  await page.route(`**/api/decks/${deckPlay.id}/live`, (route) => {
    liveReads += 1;
    return route.fulfill({ status: 500, json: { error: 'boom' } });
  });
  await stubDeckHub(page, { audience: { state: guestView('live').state } });
  await stubApi(page, { [playPath]: { json: view } });
  return () => liveReads;
}

test('player page for a non-owner never calls GET live, so no chip and no error', async ({
  mount,
  page,
}) => {
  const liveReads = await stubBrokenLive(page, { ...deckPlay, isOwner: false });

  await mount(<AppRoutes />, { hooksConfig: { route: canonical } });
  await expect(page.getByTestId('player')).toBeVisible();
  await settle(page);

  expect(liveReads()).toBe(0);
  await expect(page.getByTestId('live-chip')).toHaveCount(0);
  await expect(page.getByTestId('live-chip-error')).toHaveCount(0);
});

test('player page for the owner shows the error slot when GET live fails', async ({
  mount,
  page,
}) => {
  const liveReads = await stubBrokenLive(page, { ...deckPlay, isOwner: true });

  await mount(<AppRoutes />, { hooksConfig: { route: canonical } });

  await expect(page.getByTestId('live-chip-error')).toBeVisible();
  expect(liveReads()).toBeGreaterThan(0);
});
