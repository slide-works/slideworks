import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { guestView } from '../../test-support/fixtures/guestStates';
import { pollControlView } from '../../test-support/fixtures/pollStates';
import { POLL_SLIDE_NUMBER } from '../../test-support/fixtures/screens';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import {
  pushPollControl,
  stubPollControlApi,
  type PollControlCall,
} from '../../test-support/stubPollControlApi';
import type { PollControlView } from '../types';
import { POLL_WATCH_FAILED } from './pollMessages';
import { POLL_PANEL_TEST_IDS as IDS, pollPanelCountTestId } from '../testIds';
import { PollControlLive } from './PollControlLive';
import { PollPanel } from './PollPanel';

// The real control provider over the stub hub and the stub poll routes, read through the panel. The poll is on slide 2
// (POLL_SLIDE_NUMBER); slide 3 has none, so its poll route answers 404.

const DECK = 'deck-sw277';
const OTHER_DECK = 'deck-sw277-other';
const NO_POLL_SLIDE = 3;

const poll = (
  sample: 'draft' | 'open' | 'closed' = 'open',
  overrides: Partial<PollControlView> = {},
) => pollControlView(sample, { slide: POLL_SLIDE_NUMBER, ...overrides });

const live = (overrides: Partial<PollControlView> = {}) =>
  poll('open', {
    settings: { ...pollControlView('open').settings, results: 'live' },
    roomSeesCounts: true,
    ...overrides,
  });

function presenter(slide: number, deckId = DECK) {
  return (
    <PollControlLive deckId={deckId} slide={slide}>
      <PollPanel deckId={deckId} />
    </PollControlLive>
  );
}

async function stubs(page: Page, polls: PollControlView[] = [live()]) {
  const hub = await stubDeckHub(page, { audience: { state: guestView('live').state } });
  const api = await stubPollControlApi(page, DECK, { polls });
  return { hub, api };
}

const reads = (calls: PollControlCall[]) => calls.filter((c) => c.call === 'read');
const answered = (page: Page) => page.getByTestId(IDS.answered);

// Lets the page run its pending work, so a call it should not make has had its chance.
const settle = (page: Page) => page.evaluate(() => new Promise((done) => setTimeout(done, 300)));

test('PollControlLive_WhenMounted_ShouldWatchTheAudienceThenReadThePresentersSlide', async ({
  mount,
  page,
}) => {
  const { hub, api } = await stubs(page);
  await mount(presenter(POLL_SLIDE_NUMBER));

  await expect(page.getByTestId(IDS.state)).toHaveText('Open');
  await expect(answered(page)).toHaveText('21 answers');
  expect(hub.watches).toEqual([DECK]);
  expect(api.calls).toEqual([{ call: 'read', slide: POLL_SLIDE_NUMBER }]);
  // WatchAudience only: no presentation join and no deck join of its own.
  expect(hub.presentationJoins).toEqual([]);
  expect(hub.joined).toEqual([]);
});

test('PollControlLive_WhenTheSlideChangesToANonPollSlideAndBack_ShouldReadEachOnce', async ({
  mount,
  page,
}) => {
  const { api } = await stubs(page);
  const view = await mount(presenter(POLL_SLIDE_NUMBER));
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');

  await view.update(presenter(NO_POLL_SLIDE));
  await expect(page.getByTestId(IDS.empty)).toBeAttached();
  await expect(page.getByTestId(IDS.state)).toHaveCount(0);
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);

  await view.update(presenter(POLL_SLIDE_NUMBER));
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');
  await settle(page);
  expect(reads(api.calls).map((c) => c.slide)).toEqual([
    POLL_SLIDE_NUMBER,
    NO_POLL_SLIDE,
    POLL_SLIDE_NUMBER,
  ]);
});

test('PollControlLive_WhenPollControlArrives_ShouldFollowItAndDropALowerSeq', async ({
  mount,
  page,
}) => {
  const { hub } = await stubs(page);
  await mount(presenter(POLL_SLIDE_NUMBER));
  await expect(answered(page)).toHaveText('21 answers');

  pushPollControl(hub, live({ seq: 1000, answered: 30, counts: [5, 18, 4, 3] }));
  await expect(answered(page)).toHaveText('30 answers');
  await expect(page.getByTestId(pollPanelCountTestId(1))).toHaveText('18');

  pushPollControl(hub, live({ seq: 999, answered: 25 }));
  pushPollControl(hub, live({ seq: 1000, answered: 26 }));
  await settle(page);
  await expect(answered(page)).toHaveText('30 answers');

  // Another slide's poll changing (its round closed when the presenter moved on) doesn't touch this one.
  pushPollControl(hub, live({ seq: 1001, slide: 5, answered: 2 }));
  await settle(page);
  await expect(answered(page)).toHaveText('30 answers');
});

test('PollControlLive_WhenPollControlArrivesWhileTheReadIsHeld_ShouldKeepItAndDropTheOlderRead', async ({
  mount,
  page,
}) => {
  const { hub, api } = await stubs(page);
  const held = api.hold('read');
  await mount(presenter(POLL_SLIDE_NUMBER));
  await expect.poll(() => reads(api.calls).length).toBe(1);

  // The read's answer was stamped when it arrived; this event is newer.
  pushPollControl(hub, live({ seq: 5000, answered: 33 }));
  await expect(answered(page)).toHaveText('33 answers');

  held.release();
  await settle(page);
  await expect(answered(page)).toHaveText('33 answers');
});

test('PollControlLive_WhenAReadForASlideThePresenterLeftLandsLate_ShouldDropIt', async ({
  mount,
  page,
}) => {
  const { api } = await stubs(page);
  const first = api.hold('read');
  const view = await mount(presenter(POLL_SLIDE_NUMBER));
  await expect.poll(() => reads(api.calls).length).toBe(1);

  await view.update(presenter(NO_POLL_SLIDE));
  await expect.poll(() => reads(api.calls).length).toBe(2);
  first.release();
  await settle(page);
  await expect(page.getByTestId(IDS.state)).toHaveCount(0);

  // Back on the poll slide with its read held: had the late read been kept, the panel would show it now.
  const again = api.hold('read');
  await view.update(presenter(POLL_SLIDE_NUMBER));
  await expect.poll(() => reads(api.calls).length).toBe(3);
  await settle(page);
  await expect(page.getByTestId(IDS.state)).toHaveCount(0);
  again.release();
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');
});

test('PollControlLive_WhenTheDeckChanges_ShouldReadAgain', async ({ mount, page }) => {
  const { hub, api } = await stubs(page);
  await mount(presenter(POLL_SLIDE_NUMBER));
  await expect(answered(page)).toHaveText('21 answers');

  // An agent edits the deck: slide numbers resolve at call time, so the poll is read again.
  api.setPoll(live({ seq: 2000, answered: 22 }));
  hub.push({ type: 'deckChanged', deckId: DECK });
  await expect(answered(page)).toHaveText('22 answers');
  // Another deck's event changes nothing.
  hub.push({ type: 'deckChanged', deckId: OTHER_DECK });
  await settle(page);
  expect(reads(api.calls)).toHaveLength(2);
});

test('PollControlLive_WhenTheDeckIdChanges_ShouldWatchAndReadTheNewDeckAndShowNothingOfTheOld', async ({
  mount,
  page,
}) => {
  const { hub } = await stubs(page);
  const other = await stubPollControlApi(page, OTHER_DECK, { polls: [poll('draft')] });
  const view = await mount(presenter(POLL_SLIDE_NUMBER));
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');

  await view.update(presenter(POLL_SLIDE_NUMBER, OTHER_DECK));
  await expect(page.getByTestId(IDS.state)).toHaveText('Draft');
  expect(hub.watches).toEqual([DECK, OTHER_DECK]);
  expect(other.calls).toEqual([{ call: 'read', slide: POLL_SLIDE_NUMBER }]);
});

test('PollControlLive_WhenTheConnectionComesBack_ShouldWatchAndReadAgain', async ({
  mount,
  page,
}) => {
  const { hub, api } = await stubs(page);
  await mount(presenter(POLL_SLIDE_NUMBER));
  await expect(answered(page)).toHaveText('21 answers');

  // While offline the poll moved on; its pollControl was lost.
  api.setPoll(live({ seq: 3000, answered: 40 }));
  hub.drop();

  await expect(answered(page)).toHaveText('40 answers');
  expect(hub.watches).toEqual([DECK, DECK]);
  expect(reads(api.calls)).toHaveLength(2);
});

test('PollControlLive_WhenTheWatchFails_ShouldStillReadSayLiveUpdatesArentArrivingAndRetry', async ({
  mount,
  page,
}) => {
  const { hub, api } = await stubs(page);
  hub.fail('WatchAudience', 'Server timeout.');
  await mount(presenter(POLL_SLIDE_NUMBER));

  // The poll is read anyway, and the panel says the counts aren't live: not the no-poll text.
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');
  await expect(page.getByTestId(IDS.error)).toHaveText(POLL_WATCH_FAILED);
  expect(reads(api.calls)).toHaveLength(1);

  // The connection stayed up; the watch is tried again on a timer, and works this time.
  await expect.poll(() => hub.watches.length, { timeout: 5000 }).toBe(2);
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
  await expect.poll(() => reads(api.calls).length).toBe(2);
  pushPollControl(hub, live({ seq: 6000, answered: 34 }));
  await expect(answered(page)).toHaveText('34 answers');
});

test('PollControlLive_WhenTheWatchKeepsFailing_ShouldTryAgainOnTheNextSlideChange', async ({
  mount,
  page,
}) => {
  const { hub } = await stubs(page);
  hub.fail('WatchAudience', 'Server timeout.', { sticky: true });
  const view = await mount(presenter(POLL_SLIDE_NUMBER));
  await expect(page.getByTestId(IDS.error)).toHaveText(POLL_WATCH_FAILED);
  await expect.poll(() => hub.watches.length).toBe(1);

  // On a slide with no poll there is nothing to say.
  hub.stopFailing('WatchAudience');
  await view.update(presenter(NO_POLL_SLIDE));
  await expect.poll(() => hub.watches.length).toBe(2);
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);

  await view.update(presenter(POLL_SLIDE_NUMBER));
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
  // The watch worked: no more retries on a slide change.
  await settle(page);
  expect(hub.watches).toHaveLength(2);
});

test('PollControlLive_WhenA404LandsAfterANewerPollControl_ShouldKeepThePushedView', async ({
  mount,
  page,
}) => {
  // The slide has no poll when the read arrives, so the held read answers 404.
  const { hub, api } = await stubs(page, []);
  const held = api.hold('read');
  await mount(presenter(POLL_SLIDE_NUMBER));
  await expect.poll(() => reads(api.calls).length).toBe(1);

  // An agent made the slide a poll and opened it: its pollControl arrives before the old read's 404.
  pushPollControl(hub, live({ seq: 7000, answered: 3 }));
  await expect(answered(page)).toHaveText('3 answers');

  held.release();
  await settle(page);
  await expect(answered(page)).toHaveText('3 answers');
});

test('PollControlLive_WhenAReadAfterADeckEventFails_ShouldKeepTheRefusalMessage', async ({
  mount,
  page,
}) => {
  const { hub, api } = await stubs(page, [poll('draft')]);
  api.setLive(false);
  await mount(presenter(POLL_SLIDE_NUMBER));
  await page.getByTestId(IDS.panel).getByTestId(IDS.open).click();
  const refusal = "The deck isn't live, so the poll can't open. Go live first, then open it.";
  await expect(page.getByTestId(IDS.error)).toHaveText(refusal);
  await expect.poll(() => reads(api.calls).length).toBe(2);

  // The server stops answering reads; a deck event reads again and fails.
  await page.route(`**/api/decks/${DECK}/polls/${POLL_SLIDE_NUMBER}`, (route) =>
    route.fulfill({ status: 500, json: {} }),
  );
  let failedReads = 0;
  page.on('response', (response) => {
    if (response.url().endsWith(`/polls/${POLL_SLIDE_NUMBER}`) && response.status() === 500)
      failedReads += 1;
  });
  hub.push({ type: 'deckChanged', deckId: DECK });
  await expect.poll(() => failedReads).toBe(1);
  await settle(page);
  await expect(page.getByTestId(IDS.error)).toHaveText(refusal);
});

test('PollControlLive_WhenAnActionFailsOnTheNetwork_ShouldReadThePollAgain', async ({
  mount,
  page,
}) => {
  await stubDeckHub(page, { audience: { state: guestView('live').state } });
  const api = await stubPollControlApi(page, DECK, {
    polls: [live()],
    routes: { close: 'network-down' },
  });
  await mount(presenter(POLL_SLIDE_NUMBER));
  await page.getByTestId(IDS.panel).getByTestId(IDS.close).click();

  await expect(page.getByTestId(IDS.error)).toHaveText(
    "Couldn't close the poll. Check your connection and try again.",
  );
  await expect.poll(() => api.calls.map((c) => c.call)).toEqual(['read', 'close', 'read']);
});

test('PollControlLive_WhenEachActionIsUsed_ShouldCallItsRouteOnce', async ({ mount, page }) => {
  const manualHidden = poll('draft', {
    settings: {
      ...pollControlView('draft').settings,
      results: 'afterClose',
      correct: 2,
      opens: 'manual',
    },
    roomSeesCounts: false,
  });
  const { api } = await stubs(page, [manualHidden]);
  await mount(presenter(POLL_SLIDE_NUMBER));
  const panel = page.getByTestId(IDS.panel);

  await panel.getByTestId(IDS.open).click();
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');
  await panel.getByTestId(IDS.reveal).click();
  await expect(page.getByTestId(IDS.room)).toHaveText('Revealed: the room sees these results.');
  await panel.getByTestId(IDS.close).click();
  await expect(page.getByTestId(IDS.state)).toHaveText('Closed');
  await panel.getByTestId(IDS.showAnswer).click();
  await expect(panel.getByTestId(IDS.showAnswer)).toHaveCount(0);
  await panel.getByTestId(IDS.newRound).click();
  await expect(page.getByTestId(IDS.state)).toHaveText('Draft');
  await expect(page.getByTestId(IDS.round)).toHaveText('round 2');

  await settle(page);
  expect(api.calls.filter((c) => c.call !== 'read')).toEqual(
    (['open', 'reveal', 'close', 'showAnswer', 'newRound'] as const).map((call) => ({
      call,
      slide: POLL_SLIDE_NUMBER,
    })),
  );
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
});

test('PollControlLive_WhenOpenIsDoubleClicked_ShouldOpenOnceAndNotClose', async ({
  mount,
  page,
}) => {
  const { api } = await stubs(page, [poll('draft')]);
  await mount(presenter(POLL_SLIDE_NUMBER));
  const panel = page.getByTestId(IDS.panel);
  await expect(panel.getByTestId(IDS.open)).toBeVisible();

  await panel.getByTestId(IDS.open).dblclick();
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');
  await settle(page);
  expect(api.calls.filter((c) => c.call !== 'read').map((c) => c.call)).toEqual(['open']);
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');

  // With the call still on its way, a second press is ignored too: the button now there is Close.
  const held = api.hold('close');
  await panel.getByTestId(IDS.close).click();
  await expect(panel.getByTestId(IDS.close)).toHaveAttribute('aria-disabled', 'true');
  // force: Playwright waits for an aria-disabled button to be enabled; a person's click doesn't.
  await panel.getByTestId(IDS.close).click({ force: true });
  held.release();
  await expect(page.getByTestId(IDS.state)).toHaveText('Closed');
  await settle(page);
  expect(api.calls.filter((c) => c.call !== 'read').map((c) => c.call)).toEqual(['open', 'close']);
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
});

test('PollControlLive_WhenAnActionIsRefused_ShouldSayWhatToDoAndReadThePollAgain', async ({
  mount,
  page,
}) => {
  const { api } = await stubs(page, [poll('draft')]);
  api.setLive(false);
  await mount(presenter(POLL_SLIDE_NUMBER));
  const panel = page.getByTestId(IDS.panel);

  await panel.getByTestId(IDS.open).click();
  await expect(page.getByTestId(IDS.error)).toHaveText(
    "The deck isn't live, so the poll can't open. Go live first, then open it.",
  );
  await expect(page.getByTestId(IDS.error)).not.toContainText('not_live');
  await expect.poll(() => api.calls.map((c) => c.call)).toEqual(['read', 'open', 'read']);

  // The poll moved on elsewhere (an agent opened it): Open is refused and the buttons catch up.
  api.setLive(true);
  api.setPoll(live({ seq: 4000 }));
  await panel.getByTestId(IDS.open).click();
  await expect(page.getByTestId(IDS.error)).toHaveText(
    'The poll had already moved on. The buttons now show what you can do.',
  );
  await expect(panel.getByTestId(IDS.close)).toBeVisible();
  await expect(panel.getByTestId(IDS.open)).toHaveCount(0);
});

test('PollControlLive_WhenAnActionFails_ShouldShowAMessageAndLeaveItBehindOnAnotherSlide', async ({
  mount,
  page,
}) => {
  await stubDeckHub(page, { audience: { state: guestView('live').state } });
  const api = await stubPollControlApi(page, DECK, {
    polls: [live()],
    routes: { close: 'network-down' },
  });
  const view = await mount(presenter(POLL_SLIDE_NUMBER));
  const panel = page.getByTestId(IDS.panel);

  await panel.getByTestId(IDS.close).click();
  await expect(page.getByTestId(IDS.error)).toHaveText(
    "Couldn't close the poll. Check your connection and try again.",
  );
  await expect.poll(() => api.calls.filter((c) => c.call === 'close')).toHaveLength(1);

  // Moving on leaves the message behind.
  await view.update(presenter(NO_POLL_SLIDE));
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
});

test('PollControlLive_WhenTheReadFails_ShouldSaySo', async ({ mount, page }) => {
  await stubDeckHub(page, { audience: { state: guestView('live').state } });
  await stubPollControlApi(page, DECK, { routes: { read: { status: 500, json: {} } } });
  await mount(presenter(POLL_SLIDE_NUMBER));

  await expect(page.getByTestId(IDS.error)).toHaveText(
    "Couldn't load this slide's poll. Check your connection; it loads again when you move or reconnect.",
  );
});
