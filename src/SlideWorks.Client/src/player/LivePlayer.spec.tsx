import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { LiveView } from '../live/liveView';
import type { PresentationConnection, PresentationState } from '../presentation/types';
import { JoinProvider } from '../slides/JoinContext';
import { guestView } from '../test-support/fixtures/guestStates';
import { presentationState } from '../test-support/fixtures/presenting';
import { deckPlay } from '../test-support/fixtures/screens';
import { stubDeckHub } from '../test-support/stubDeckHub';
import { Player } from './Player';

// The owner's plain player while the deck is live: one more presenter-role client of the presentation. It follows the
// state it finds, publishes only the moves made in it, and never overwrites a presentation another window drives.

// The sample deck's three slides repeated to six, so a few presses in a row stay inside the deck.
const deck = {
  ...deckPlay,
  slides: Array.from({ length: 6 }, (_, i) => ({ ...deckPlay.slides[i % 3], number: i + 1 })),
};
const join = { ...deckPlay.join, live: true };
const open: LiveView = {
  sessionId: 'session-1',
  code: join.code,
  codeLabel: join.codeLabel,
  joinUrl: join.joinUrl,
  hostLabel: join.hostLabel,
  live: true,
  joinedCount: 3,
  joinBar: false,
};
const window1: PresentationConnection = {
  clientId: 'k-1',
  role: 'presenter',
  label: 'Chrome on Linux',
};
const phone: PresentationConnection = { clientId: 'k-3', role: 'phone', label: 'Phone' };
const audience: PresentationConnection = { clientId: 'k-2', role: 'audience', label: 'Room' };

let seq = 200;
const stateAt = (slide: number, overrides: Partial<PresentationState> = {}) =>
  presentationState({
    deckId: deckPlay.id,
    seq: ++seq,
    slide,
    step: 0,
    timer: { state: 'paused', elapsedMs: 760000 },
    connected: [],
    ...overrides,
  });

const count = (page: Page) => page.getByTestId('player-count');
// Lets the page run its pending work, so a call it should not make has had its chance.
const settle = (page: Page) => page.evaluate(() => new Promise((done) => setTimeout(done, 400)));

async function mountLivePlayer(
  mount: (component: React.JSX.Element) => Promise<unknown>,
  page: Page,
  state: PresentationState,
  hash = '',
  options: { holdUpdates?: boolean; holdJoin?: boolean } = {},
) {
  await page.evaluate((h) => (window.location.hash = h), hash);
  await page.route(`**/api/decks/${deckPlay.id}/live`, (request) =>
    request.fulfill({ json: open }),
  );
  const hub = await stubDeckHub(page, {
    presentation: { state, ...options },
    audience: { state: guestView('live').state },
  });
  await mount(
    <JoinProvider deckId={deckPlay.id} join={join} owner>
      <Player deck={deck} />
    </JoinProvider>,
  );
  await expect(page.getByTestId('live-chip-end')).toBeVisible();
  return hub;
}

test('LivePlayer_WhenAPresenterWindowIsDriving_ShouldAdoptItsPositionAndPublishNothing', async ({
  mount,
  page,
}) => {
  const hub = await mountLivePlayer(
    mount,
    page,
    stateAt(3, { black: true, connected: [window1, audience] }),
    '#1',
  );

  await expect(count(page)).toContainText('3');
  await expect(page.getByTestId('slide-3')).toBeVisible();
  await settle(page);
  expect(hub.presentationJoins[0]).toMatchObject({ role: 'presenter', label: 'Player' });
  expect(hub.updates).toEqual([]);
});

test('LivePlayer_WhenAPhoneIsDriving_ShouldAdoptItsPositionAndPublishNothing', async ({
  mount,
  page,
}) => {
  const hub = await mountLivePlayer(mount, page, stateAt(2, { connected: [phone] }), '#3');

  await expect(page.getByTestId('slide-2')).toBeVisible();
  await settle(page);
  expect(hub.updates).toEqual([]);
});

test('LivePlayer_WhenMovedWhileAWindowDrives_ShouldPublishTheMoveKeepingTheBlackScreen', async ({
  mount,
  page,
}) => {
  const hub = await mountLivePlayer(mount, page, stateAt(2, { black: true, connected: [window1] }));
  await expect(page.getByTestId('slide-2')).toBeVisible();

  await page.keyboard.press('ArrowRight');

  await expect(page.getByTestId('slide-3')).toBeVisible();
  expect(hub.updates).toEqual([{ slide: 3, step: 0, black: true, skipDeepDives: false }]);
});

const untouched = (overrides: Partial<PresentationState> = {}) =>
  stateAt(1, { timer: { state: 'notStarted', elapsedMs: 0 }, ...overrides });

test('LivePlayer_WhenNobodyIsConnectedAndTheTalkIsUntouched_ShouldStartItAtItsOwnPositionOnce', async ({
  mount,
  page,
}) => {
  const hub = await mountLivePlayer(
    mount,
    page,
    untouched({ connected: [audience], skipDeepDives: true }),
    '#2',
  );

  await expect(page.getByTestId('slide-2')).toBeVisible();
  await expect
    .poll(() => hub.updates)
    .toEqual([{ slide: 2, step: 0, black: false, skipDeepDives: true }]);
  await settle(page);
  expect(hub.updates).toHaveLength(1);
});

test('LivePlayer_WhenNobodyIsConnectedButTheTalkIsPausedMidway_ShouldAdoptItAndPublishNothing', async ({
  mount,
  page,
}) => {
  const hub = await mountLivePlayer(mount, page, stateAt(3, { connected: [audience] }), '#1');

  await expect(page.getByTestId('slide-3')).toBeVisible();
  await settle(page);
  expect(hub.updates).toEqual([]);
});

test('LivePlayer_WhenNobodyIsConnectedButTheScreenIsBlack_ShouldAdoptItAndPublishNothing', async ({
  mount,
  page,
}) => {
  const hub = await mountLivePlayer(mount, page, untouched({ black: true }), '#2');

  await expect(page.getByTestId('slide-1')).toBeVisible();
  await settle(page);
  expect(hub.updates).toEqual([]);
});

test('LivePlayer_WhenAWindowIsConnectedToAnUntouchedTalk_ShouldAdoptItAndPublishNothing', async ({
  mount,
  page,
}) => {
  const hub = await mountLivePlayer(mount, page, untouched({ connected: [window1] }), '#2');

  await expect(page.getByTestId('slide-1')).toBeVisible();
  await settle(page);
  expect(hub.updates).toEqual([]);
});

test('LivePlayer_WhenKeysArePressedInsideOneRoundTrip_ShouldSendEachMoveFromTheLastTarget', async ({
  mount,
  page,
}) => {
  const hub = await mountLivePlayer(mount, page, stateAt(2, { connected: [window1] }), '', {
    holdUpdates: true,
  });
  await expect(page.getByTestId('slide-2')).toBeVisible();

  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowRight');

  await expect.poll(() => hub.updates.length).toBe(3);
  const targets = hub.updates.map((u) => `${u.slide}.${u.step}`);
  expect(new Set(targets).size).toBe(3);
  hub.releaseUpdates();
  const last = hub.updates[2];
  await expect(page.getByTestId(`slide-${last.slide}`)).toBeVisible();
  // The third target is where the talk is: slide 2 has moved on by three steps or slides.
  expect(last.slide * 100 + last.step).toBeGreaterThan(200);
});

test('LivePlayer_WhenAnotherClientMovesAfterAPress_ShouldMoveOnFromTheirPosition', async ({
  mount,
  page,
}) => {
  const hub = await mountLivePlayer(mount, page, stateAt(2, { connected: [window1] }));
  await expect(page.getByTestId('slide-2')).toBeVisible();
  await page.keyboard.press('ArrowRight');
  await expect.poll(() => hub.updates.length).toBe(1);
  const before = hub.updates[0];

  hub.pushState(stateAt(4, { seq: 900, connected: [window1] }));
  await expect(page.getByTestId('slide-4')).toBeVisible();
  await page.keyboard.press('ArrowRight');

  await expect.poll(() => hub.updates.length).toBe(2);
  expect(hub.updates[1].slide * 100 + hub.updates[1].step).toBeGreaterThan(400);
  expect(before.slide).toBeLessThan(4);
});

test('LivePlayer_WhenAKeyIsPressedWhileTheFirstPublishIsInFlight_ShouldPublishItAfterTheSeed', async ({
  mount,
  page,
}) => {
  const hub = await mountLivePlayer(mount, page, untouched({ connected: [audience] }), '#2', {
    holdUpdates: true,
  });
  await expect.poll(() => hub.updates.length).toBe(1);

  await page.keyboard.press('ArrowRight');

  await expect.poll(() => hub.updates.length).toBe(2);
  expect(hub.updates[0]).toMatchObject({ slide: 2, step: 0 });
  expect(hub.updates[1].slide * 100 + hub.updates[1].step).toBeGreaterThan(200);
  hub.releaseUpdates();
  await expect(page.getByTestId(`slide-${hub.updates[1].slide}`)).toBeVisible();
});

test('LivePlayer_WhenTheConnectionIsDown_ShouldFreezeTheMovesAndShowTheReconnectingNotice', async ({
  mount,
  page,
}) => {
  const hub = await mountLivePlayer(mount, page, stateAt(2, { connected: [window1] }), '', {
    holdJoin: true,
  });
  await expect.poll(() => hub.presentationJoins.length).toBe(1);
  hub.releasePresentationJoins();
  await expect(page.getByTestId('slide-2')).toBeVisible();
  await expect(page.getByTestId('player-reconnecting')).toHaveCount(0);

  hub.drop();
  await expect(page.getByTestId('player-reconnecting')).toContainText('Reconnecting');
  await page.keyboard.press('ArrowRight');
  await page
    .getByTestId('player-next')
    .click({ force: true })
    .catch(() => undefined);
  await settle(page);

  await expect(page.getByTestId('slide-2')).toBeVisible();
  expect(hub.updates).toEqual([]);
  await expect.poll(() => hub.presentationJoins.length).toBe(2);
  hub.pushState(stateAt(4, { seq: 900, connected: [window1] }));
  hub.releasePresentationJoins();
  await expect(page.getByTestId('slide-4')).toBeVisible();
  await expect(page.getByTestId('player-reconnecting')).toHaveCount(0);
  expect(hub.updates).toEqual([]);
});

test('LivePlayer_WhenAMovePublishFails_ShouldShowAMessageInTheChip', async ({ mount, page }) => {
  const hub = await mountLivePlayer(mount, page, stateAt(2, { connected: [window1] }));
  await expect(page.getByTestId('slide-2')).toBeVisible();
  hub.fail('UpdatePresentation', 'boom');

  await page.keyboard.press('ArrowRight');

  await expect(page.getByTestId('live-chip-error')).toContainText('could not publish');
});

test('LivePlayer_WhenTheConnectionDropsAndAWindowMovedMeanwhile_ShouldAdoptItAndPublishNothing', async ({
  mount,
  page,
}) => {
  const hub = await mountLivePlayer(mount, page, stateAt(2, { connected: [window1] }));
  await expect(page.getByTestId('slide-2')).toBeVisible();

  hub.pushState(stateAt(3, { black: true, connected: [window1] }));
  await expect(page.getByTestId('slide-3')).toBeVisible();
  hub.drop();
  hub.pushState(stateAt(1, { black: true, connected: [window1] }));
  await expect.poll(() => hub.presentationJoins.length).toBe(2);

  await expect(page.getByTestId('slide-1')).toBeVisible();
  await settle(page);
  expect(hub.updates).toEqual([]);
});

test('LivePlayer_WhenTheDeckEnds_ShouldLeaveThePresentationAndMoveLocally', async ({
  mount,
  page,
}) => {
  const hub = await mountLivePlayer(mount, page, stateAt(2, { connected: [window1] }));
  await expect(page.getByTestId('slide-2')).toBeVisible();

  await page.getByTestId('live-chip-end').click();
  await expect.poll(() => hub.leaves).toEqual([deckPlay.id]);
  await page.keyboard.press('ArrowRight');

  await expect(page.getByTestId('slide-3')).toBeVisible();
  expect(hub.updates).toEqual([]);
});

test('LivePlayer_WhenThePresentationRefusesTheJoin_ShouldShowAMessageInTheChip', async ({
  mount,
  page,
}) => {
  await page.route(`**/api/decks/${deckPlay.id}/live`, (request) =>
    request.fulfill({ json: open }),
  );
  await stubDeckHub(page, {
    presentation: { state: stateAt(2), refuse: true },
    audience: { state: guestView('live').state },
  });
  await mount(
    <JoinProvider deckId={deckPlay.id} join={join} owner>
      <Player deck={deckPlay} />
    </JoinProvider>,
  );

  await expect(page.getByTestId('live-chip-error')).toContainText('refused');
});
