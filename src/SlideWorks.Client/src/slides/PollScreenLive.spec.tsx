import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { DeckPlaySlide } from '../api/types';
import { AppRoutes } from '../AppRoutes';
import type { PollControlView } from '../polls/types';
import {
  POLL_SLIDE_TEST_IDS as IDS,
  pollSlideBarTestId,
  pollSlidePercentTestId,
} from '../polls/testIds';
import { guestView } from '../test-support/fixtures/guestStates';
import { lookViewOf } from '../test-support/fixtures/lookViewOf';
import { pollControlView } from '../test-support/fixtures/pollStates';
import { presentationState } from '../test-support/fixtures/presenting';
import { POLL_SLIDE_NUMBER, deckPlayWithPoll } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';
import { stubDeckHub } from '../test-support/stubDeckHub';
import { pushPollControl, stubPollControlApi } from '../test-support/stubPollControlApi';
import { JoinProvider } from './JoinContext';
import { PollScreenLive } from './PollScreenLive';
import { SlideRenderer } from './SlideRenderer';
import type { SlideDeck } from './slots';

test.use({ viewport: { width: 1920, height: 1080 } });

// The real screen provider over the stub hub and the stub poll routes. The fixture deck's slide 2 is the poll; slide 1
// is not. The poll's view is the sample's open round on slide 2 with the counts the room sees.
const deckView = deckPlayWithPoll();
const deckId = deckView.id;
const liveJoin = { ...deckView.join, live: true };
const SLIDES = deckView.slides.slice(0, 2);
const deck: SlideDeck = {
  title: deckView.title,
  author: deckView.author,
  parts: [],
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

const pollView = (overrides: Partial<PollControlView> = {}) =>
  pollControlView('open', {
    slide: POLL_SLIDE_NUMBER,
    roomSeesCounts: true,
    answered: 38,
    counts: [7, 17, 10, 4],
    ...overrides,
  });

let stateSeq = 500;
const stateAt = (slide: number) => presentationState({ deckId, seq: ++stateSeq, slide, step: 0 });

// The deck is live and the viewer is confirmed as its owner by GET /live.
async function stubLive(page: Page, polls: PollControlView[] = [pollView()]) {
  const hub = await stubDeckHub(page, { audience: { state: guestView('live').state } });
  await stubApi(page, {
    [`/api/decks/${deckId}/live`]: {
      json: {
        sessionId: 'session-1',
        code: liveJoin.code,
        codeLabel: liveJoin.codeLabel,
        joinUrl: liveJoin.joinUrl,
        hostLabel: liveJoin.hostLabel,
        live: true,
        joinedCount: 41,
        joinBar: false,
      },
    },
  });
  const pollApi = await stubPollControlApi(page, deckId, { polls });
  return { hub, polls: pollApi };
}

function player(owner: boolean, slides: DeckPlaySlide[] = SLIDES) {
  return (
    <JoinProvider deckId={deckId} join={liveJoin} owner={owner}>
      <PollScreenLive deckId={deckId} owner={owner}>
        {slides.map((slide) => (
          <SlideRenderer key={slide.number} slide={slide} deck={deck} total={6} step={0} />
        ))}
      </PollScreenLive>
    </JoinProvider>
  );
}

const pollSlide = (page: Page) => page.getByTestId(`slide-${POLL_SLIDE_NUMBER}`);
const percent = (page: Page, position: number) =>
  pollSlide(page).getByTestId(pollSlidePercentTestId(position));

// Lets the page run its pending work, so a call or render it should not make has had its chance.
const settle = (page: Page) => page.evaluate(() => new Promise((done) => setTimeout(done, 300)));

// Both watches are in: the live session's and the screen provider's.
async function watching(hub: Awaited<ReturnType<typeof stubDeckHub>>) {
  await expect.poll(() => hub.watches.length).toBeGreaterThanOrEqual(2);
}

test('a presenter sees the bars from the GET when the presenter arrives on the poll slide', async ({
  mount,
  page,
}) => {
  const { hub, polls } = await stubLive(page);
  await mount(player(true));
  await watching(hub);
  expect(hub.watches).toContain(deckId);
  await expect(pollSlide(page).getByTestId(IDS.state)).toHaveCount(0);

  hub.pushState(stateAt(POLL_SLIDE_NUMBER));
  await expect(percent(page, 1)).toHaveText('45%');
  await expect(pollSlide(page).getByTestId(IDS.state)).toHaveText('Open');
  await expect(pollSlide(page).getByTestId(IDS.answered)).toHaveText('38 answers');
  expect(polls.calls).toEqual([{ call: 'read', slide: POLL_SLIDE_NUMBER }]);
});

test('no poll is read for a slide that is not a poll', async ({ mount, page }) => {
  const { hub, polls } = await stubLive(page);
  await mount(player(true));
  await watching(hub);
  hub.pushState(stateAt(1));
  await settle(page);
  expect(polls.calls).toEqual([]);
  await expect(pollSlide(page).getByTestId(IDS.state)).toHaveCount(0);
});

test('after the GET, pollControl moves the bars, and a lower seq is dropped', async ({
  mount,
  page,
}) => {
  const { hub } = await stubLive(page);
  await mount(player(true));
  await watching(hub);
  hub.pushState(stateAt(POLL_SLIDE_NUMBER));
  await expect(percent(page, 1)).toHaveText('45%');

  pushPollControl(hub, pollView({ seq: 1_000_000, answered: 40, counts: [10, 20, 6, 4] }));
  await expect(percent(page, 1)).toHaveText('50%');
  await expect(pollSlide(page).getByTestId(IDS.answered)).toHaveText('40 answers');

  // Late: an older view than the one on screen.
  pushPollControl(hub, pollView({ seq: 999_999, answered: 20, counts: [20, 0, 0, 0] }));
  await settle(page);
  await expect(percent(page, 1)).toHaveText('50%');
  await expect(pollSlide(page).getByTestId(IDS.answered)).toHaveText('40 answers');

  // The newest wins again.
  pushPollControl(hub, pollView({ seq: 1_000_001, phase: 'closed', answered: 40 }));
  await expect(pollSlide(page).getByTestId(IDS.state)).toHaveText('Closed');
});

test('counts stay off the slide while the room cannot see them; the answer count still shows', async ({
  mount,
  page,
}) => {
  const hidden = pollView({ roomSeesCounts: false, answered: 21, counts: [5, 9, 4, 3] });
  const { hub } = await stubLive(page, [hidden]);
  await mount(player(true));
  await watching(hub);
  hub.pushState(stateAt(POLL_SLIDE_NUMBER));
  await expect(pollSlide(page).getByTestId(IDS.hiddenNote)).toBeVisible();
  await expect(pollSlide(page).getByTestId(IDS.answered)).toHaveText('21 answers');
  for (const position of [0, 1, 2, 3]) {
    await expect(percent(page, position)).toBeEmpty();
    await expect(pollSlide(page).getByTestId(pollSlideBarTestId(position))).toHaveCount(0);
  }
});

test('the bars change without moving focus', async ({ mount, page }) => {
  const { hub } = await stubLive(page);
  await mount(
    <div>
      <button type="button" data-testid="elsewhere">
        Elsewhere
      </button>
      {player(true)}
    </div>,
  );
  await watching(hub);
  await page.getByTestId('elsewhere').focus();
  hub.pushState(stateAt(POLL_SLIDE_NUMBER));
  await expect(percent(page, 1)).toHaveText('45%');
  pushPollControl(hub, pollView({ seq: 1_000_000, answered: 40, counts: [10, 20, 6, 4] }));
  await expect(percent(page, 1)).toHaveText('50%');
  await expect(page.getByTestId('elsewhere')).toBeFocused();
});

test('after a reconnect it watches again and reads the poll again', async ({ mount, page }) => {
  const { hub, polls } = await stubLive(page);
  await mount(player(true));
  await watching(hub);
  hub.pushState(stateAt(POLL_SLIDE_NUMBER));
  await expect(percent(page, 1)).toHaveText('45%');
  const watchesBefore = hub.watches.length;

  polls.setPoll(pollView({ answered: 40, counts: [10, 20, 6, 4] }));
  hub.drop();
  await expect.poll(() => hub.watches.length).toBeGreaterThan(watchesBefore);
  await expect(percent(page, 1)).toHaveText('50%');
  await expect.poll(() => polls.calls.length).toBe(2);
});

test('a viewer who cannot present gets empty tracks and makes no call', async ({ mount, page }) => {
  const { hub, polls } = await stubLive(page);
  await mount(player(false));
  await expect(pollSlide(page).getByTestId(IDS.question)).toBeVisible();
  await settle(page);
  expect(hub.watches).toEqual([]);
  expect(polls.calls).toEqual([]);
  await expect(pollSlide(page).getByTestId(IDS.state)).toHaveCount(0);
  await expect(percent(page, 0)).toBeEmpty();
});

test('the audience window of the deck owner shows the live poll on the presenter slide', async ({
  mount,
  page,
}) => {
  const owned = { ...deckPlayWithPoll(), isOwner: true, join: liveJoin };
  const hub = await stubDeckHub(page, {
    presentation: { state: stateAt(1) },
    audience: { state: guestView('live').state },
  });
  await stubApi(page, {
    [`/api/play/${deckId}`]: { json: owned },
    [`/api/decks/${deckId}/live`]: {
      json: {
        sessionId: 'session-1',
        code: liveJoin.code,
        codeLabel: liveJoin.codeLabel,
        joinUrl: liveJoin.joinUrl,
        hostLabel: liveJoin.hostLabel,
        live: true,
        joinedCount: 41,
        joinBar: false,
      },
    },
  });
  const polls = await stubPollControlApi(page, deckId, { polls: [pollView()] });
  await mount(<AppRoutes />, {
    hooksConfig: { route: `/d/${deckId}/${owned.slug}?present` },
  });
  await expect(page.getByTestId('audience-mode')).toBeVisible();
  await watching(hub);

  hub.pushState(stateAt(POLL_SLIDE_NUMBER));
  await expect(pollSlide(page)).toBeVisible();
  await expect(percent(page, 1)).toHaveText('45%');
  await expect(pollSlide(page).getByTestId(IDS.join)).toBeVisible();
  expect(polls.calls).toEqual([{ call: 'read', slide: POLL_SLIDE_NUMBER }]);
});
