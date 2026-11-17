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
) {
  await page.evaluate((h) => (window.location.hash = h), hash);
  await page.route(`**/api/decks/${deckPlay.id}/live`, (request) =>
    request.fulfill({ json: open }),
  );
  const hub = await stubDeckHub(page, {
    presentation: { state },
    audience: { state: guestView('live').state },
  });
  await mount(
    <JoinProvider deckId={deckPlay.id} join={join} owner>
      <Player deck={deckPlay} />
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

test('LivePlayer_WhenNoWindowIsDriving_ShouldStartThePresentationAtItsOwnPositionOnce', async ({
  mount,
  page,
}) => {
  const hub = await mountLivePlayer(mount, page, stateAt(1, { connected: [audience] }), '#2');

  await expect(page.getByTestId('slide-2')).toBeVisible();
  await expect
    .poll(() => hub.updates)
    .toEqual([{ slide: 2, step: 0, black: false, skipDeepDives: false }]);
  await settle(page);
  expect(hub.updates).toHaveLength(1);
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
