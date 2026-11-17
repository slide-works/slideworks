import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { DeckPlayView } from '../api/types';
import { AppRoutes } from '../AppRoutes';
import type { PresentationConnection, PresentationState } from '../presentation/types';
import { deckPlay } from '../test-support/fixtures/screens';
import { presentationState } from '../test-support/fixtures/presenting';
import { stubApi } from '../test-support/stubApi';
import { stubDeckHub } from '../test-support/stubDeckHub';

const playPath = `/api/play/${deckPlay.id}`;
const audienceRoute = `/d/${deckPlay.id}/${deckPlay.slug}?present`;
const TOTAL = deckPlay.slides.length;

const presenter: PresentationConnection = { clientId: 'k-1', role: 'presenter', label: 'Laptop' };
const phone: PresentationConnection = { clientId: 'k-3', role: 'phone', label: 'Phone' };
const audience: PresentationConnection = { clientId: 'k-2', role: 'audience', label: 'Room' };

// The sample deck is "Ship smaller": 3 slides, so these positions exist.
// Every state is newer than the last (P1): a push with the same seq is not applied.
let seq = 100;
const stateAt = (slide: number, overrides: Partial<PresentationState> = {}) =>
  presentationState({
    deckId: deckPlay.id,
    seq: ++seq,
    slide,
    step: 0,
    connected: [presenter, audience, phone],
    ...overrides,
  });

const count = (page: Page) => page.getByTestId('player-count');

async function openAudience(page: Page, state: PresentationState, options = {}) {
  await stubApi(page, { [playPath]: { json: deckPlay } });
  const hub = await stubDeckHub(page, { presentation: { state, ...options } });
  return hub;
}

// A state from a presenter has arrived: the page is live and driven, so its controls are gone.
const joined = (page: Page) => expect(page.getByTestId('player-controls')).toHaveCount(0);

// Lets the page run its pending work, so a call it should not make has had its chance.
const settle = (page: Page) => page.evaluate(() => new Promise((done) => setTimeout(done, 300)));

test('audience mode follows pushed states and mirrors them in the hash', async ({
  mount,
  page,
}) => {
  const hub = await openAudience(page, stateAt(2));
  await mount(<AppRoutes />, { hooksConfig: { route: audienceRoute } });

  await expect(page.getByTestId('audience-mode')).toBeVisible();
  await expect(page.getByTestId('player')).toBeVisible();
  await expect(page.getByTestId('slide-2')).toBeVisible();
  expect(hub.presentationJoins[0]).toMatchObject({ role: 'audience', deckId: deckPlay.id });

  hub.pushState(stateAt(3));
  await expect.poll(() => page.evaluate(() => window.location.hash)).toBe('#3');
  await expect(page.getByTestId('slide-3')).toBeVisible();
});

test('audience mode hides the controls while driven and shows them when only audience windows remain', async ({
  mount,
  page,
}) => {
  const hub = await openAudience(page, stateAt(2));
  await mount(<AppRoutes />, { hooksConfig: { route: audienceRoute } });
  await expect(page.getByTestId('slide-2')).toBeVisible();
  await expect(page.getByTestId('player-controls')).toHaveCount(0);

  hub.pushState(stateAt(2, { connected: [audience] }));
  await expect(page.getByTestId('player-controls')).toBeVisible();
  await expect(count(page)).toHaveText(`2 / ${TOTAL}`);

  hub.pushState(stateAt(2, { connected: [audience, phone] }));
  await expect(page.getByTestId('player-controls')).toHaveCount(0);
});

test('audience mode ignores clicks on the stage while driven', async ({ mount, page }) => {
  const hub = await openAudience(page, stateAt(2));
  await mount(<AppRoutes />, { hooksConfig: { route: audienceRoute } });
  await expect(page.getByTestId('slide-2')).toBeVisible();

  const stage = page.getByTestId('player-stage');
  const box = (await stage.boundingBox())!;
  await page.mouse.click(box.x + box.width * 0.9, box.y + box.height / 2);
  await page.mouse.click(box.x + box.width * 0.1, box.y + box.height / 2);
  await settle(page);

  expect(hub.updates).toEqual([]);
  await expect(page.getByTestId('slide-2')).toBeVisible();
});

test('audience mode keys send updates and render the answer', async ({ mount, page }) => {
  const hub = await openAudience(page, stateAt(1, { skipDeepDives: true }));
  await mount(<AppRoutes />, { hooksConfig: { route: audienceRoute } });
  await expect(page.getByTestId('slide-1')).toBeVisible();
  await joined(page);

  await page.keyboard.press('ArrowRight');
  await expect(page.getByTestId('slide-2')).toBeVisible();
  expect(hub.updates).toEqual([{ slide: 2, step: 0, black: false, skipDeepDives: true }]);

  // Slide 3 is a deep dive, and the presentation skips them: nothing to send.
  await page.keyboard.press('ArrowRight');
  await settle(page);
  expect(hub.updates).toHaveLength(1);
  await expect(page.getByTestId('slide-2')).toBeVisible();

  await page.keyboard.press('Home');
  await expect(page.getByTestId('slide-1')).toBeVisible();
  await page.keyboard.press('End');
  await expect(page.getByTestId('slide-3')).toBeVisible();
  expect(hub.updates.at(-1)).toMatchObject({ slide: 3, step: 0 });
});

test('audience mode moves locally until the join, and the next state overrides it', async ({
  mount,
  page,
}) => {
  const hub = await openAudience(page, stateAt(3), { holdJoin: true });
  await mount(<AppRoutes />, { hooksConfig: { route: audienceRoute } });
  await expect(page.getByTestId('slide-1')).toBeVisible();

  await page.keyboard.press('ArrowRight');
  await expect(page.getByTestId('slide-2')).toBeVisible();
  expect(hub.updates).toEqual([]);

  hub.releasePresentationJoins();
  await expect(page.getByTestId('slide-3')).toBeVisible();
});

test('audience mode shows a message when an update fails', async ({ mount, page }) => {
  const hub = await openAudience(page, stateAt(1));
  await mount(<AppRoutes />, { hooksConfig: { route: audienceRoute } });
  await expect(page.getByTestId('slide-1')).toBeVisible();
  await joined(page);

  hub.fail('UpdatePresentation', 'Not presenting');
  await page.keyboard.press('ArrowRight');
  // Inside the player section, so it shows when that section is fullscreen.
  await expect(page.getByTestId('player').getByTestId('audience-error')).toBeVisible();
  await expect(page.getByTestId('slide-1')).toBeVisible();

  await page.keyboard.press('ArrowRight');
  await expect(page.getByTestId('slide-2')).toBeVisible();
  await expect(page.getByTestId('audience-error')).toHaveCount(0);
});

test('audience mode covers the stage in black while blacked out', async ({ mount, page }) => {
  const hub = await openAudience(page, stateAt(2, { black: true }));
  await mount(<AppRoutes />, { hooksConfig: { route: audienceRoute } });

  await expect(page.getByTestId('blackout')).toBeVisible();
  const stage = (await page.getByTestId('player-stage').boundingBox())!;
  const cover = (await page.getByTestId('blackout').boundingBox())!;
  expect(cover).toEqual(stage);

  hub.pushState(stateAt(2, { black: false }));
  await expect(page.getByTestId('blackout')).toHaveCount(0);
});

test('audience mode falls back to the plain player when the join is refused', async ({
  mount,
  page,
}) => {
  const hub = await openAudience(page, stateAt(2), { refuse: true });
  await mount(<AppRoutes />, { hooksConfig: { route: audienceRoute } });

  await expect(page.getByTestId('player-controls')).toBeVisible();
  await expect.poll(() => hub.presentationJoins).toHaveLength(1);
  expect(hub.presentationJoins[0]).toMatchObject({ role: 'audience', deckId: deckPlay.id });
  await expect(page.getByTestId('audience-mode')).toHaveCount(0);
  await expect(count(page)).toHaveText(`1 / ${TOTAL}`);
  await page.keyboard.press('ArrowRight');
  await expect(count(page)).toHaveText(`2 / ${TOTAL}`);
  await page.getByTestId('player-next').click();
  await expect(count(page)).toHaveText(`3 / ${TOTAL}`);
  expect(hub.updates).toEqual([]);
});

test('audience mode refetches the deck on DeckChanged over the one socket', async ({
  mount,
  page,
}) => {
  const longer: DeckPlayView = {
    ...deckPlay,
    slides: [...deckPlay.slides, { ...deckPlay.slides[1], number: 4 }],
  };
  const hub = await stubDeckHub(page, { presentation: { state: stateAt(2) } });
  let reads = 0;
  await page.route(`**${playPath}`, (route) => {
    reads += 1;
    return route.fulfill({ json: reads <= 2 ? deckPlay : longer });
  });
  await mount(<AppRoutes />, { hooksConfig: { route: audienceRoute } });

  await expect(page.getByTestId('slide-3')).toHaveCount(1);
  await expect(page.getByTestId('slide-4')).toHaveCount(0);
  await expect.poll(() => hub.joined).toEqual([deckPlay.id]);
  await expect.poll(() => reads).toBe(2);

  hub.push({ type: 'deckChanged', deckId: deckPlay.id });
  await expect(page.getByTestId('slide-4')).toHaveCount(1);
  await expect(page.getByTestId('slide-2')).toBeVisible();
  expect(hub.sockets).toBe(1);
});

test('audience mode freezes while offline, moves locally, and the live state wins on reconnect', async ({
  mount,
  page,
}) => {
  const hub = await openAudience(page, stateAt(2, { black: true }));
  await mount(<AppRoutes />, { hooksConfig: { route: audienceRoute } });
  await expect(page.getByTestId('slide-2')).toBeVisible();
  await joined(page);

  // The connection drops and the rejoin fails, so the window stays offline.
  hub.fail('JoinPresentation', 'Server busy', { sticky: true });
  hub.drop();
  await expect.poll(() => hub.sockets).toBe(2);
  await expect(page.getByTestId('slide-2')).toBeVisible();
  await expect(page.getByTestId('blackout')).toBeVisible();
  await expect(page.getByTestId('player-controls')).toHaveCount(0);

  await page.keyboard.press('ArrowRight');
  await expect(page.getByTestId('slide-3')).toBeVisible();
  expect(hub.updates).toEqual([]);

  // Back online to the same presentation state: it wins over the local move.
  hub.stopFailing('JoinPresentation');
  await expect(page.getByTestId('slide-2')).toBeVisible({ timeout: 15_000 });
  await expect.poll(() => page.evaluate(() => window.location.hash)).toBe('#2');
});

test('audience mode keeps ?present through the redirect to the deck slug', async ({
  mount,
  page,
}) => {
  const hub = await openAudience(page, stateAt(2));
  await mount(<AppRoutes />, { hooksConfig: { route: `/d/${deckPlay.id}?present` } });

  await expect(page.getByTestId('audience-mode')).toBeVisible();
  await expect(page.getByTestId('slide-2')).toBeVisible();
  expect(hub.presentationJoins).toHaveLength(1);
});

test('audience mode sends nothing at the end of the deck', async ({ mount, page }) => {
  const hub = await openAudience(page, stateAt(3));
  await mount(<AppRoutes />, { hooksConfig: { route: audienceRoute } });
  await expect(page.getByTestId('slide-3')).toBeVisible();
  await joined(page);

  await page.keyboard.press('ArrowRight');
  await settle(page);
  expect(hub.updates).toEqual([]);
});
