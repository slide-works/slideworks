import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { DeckPlaySlide } from '../../api/types';
import { OpenResponseScreenLive } from '../../openResponses/OpenResponseScreenLive';
import type { OpenResponseControlView, WordCloudEvent } from '../../openResponses/types';
import { guestView } from '../../test-support/fixtures/guestStates';
import { LAYOUT_SLOTS } from '../../test-support/fixtures/layoutSlots';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { deckPlay } from '../../test-support/fixtures/screens';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { JoinProvider } from '../JoinContext';
import { SlideRenderer } from '../SlideRenderer';
import type { SlideDeck } from '../slots';

test.use({ viewport: { width: 1920, height: 1080 } });

// The layout under the real screen provider, over the stub hub and a stubbed GET of the slide's control view. Slide 2 of
// the fixture deck is the word cloud.
const SLIDE = 2;
const deckId = deckPlay.id;
const liveJoin = { ...deckPlay.join, live: true };
const QUESTION = LAYOUT_SLOTS['word-cloud'].question;
const cloudSlide: DeckPlaySlide = {
  ...deckPlay.slides[SLIDE - 1],
  layout: 'word-cloud',
  steps: 0,
  slots: structuredClone(LAYOUT_SLOTS['word-cloud']),
} as DeckPlaySlide;
const deck: SlideDeck = {
  title: deckPlay.title,
  author: deckPlay.author,
  parts: [],
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

const ROUND = '6e2f9a14-3c7b-4d58-a1e0-9b4c7d2f5a63';

function control(revision: number, entries: OpenResponseControlView['entries']): OpenResponseControlView {
  return {
    kind: 'word-cloud',
    phase: 'open',
    round: ROUND,
    number: 1,
    revision,
    results: 'live',
    opens: 'arrival',
    revealed: true,
    guests: entries.filter((entry) => !entry.hidden).length,
    entries,
  };
}

// The deck is live and the slide's control view is `view`. GET /live confirms the viewer as someone who can present, or
// with canPresent false answers 403 as it does for anyone else; liveAnswers counts the answers it has sent. Each GET of
// a slide's view is recorded by its slide number.
async function stubLive(page: Page, view: OpenResponseControlView, { canPresent = true } = {}) {
  const hub = await stubDeckHub(page, { audience: { state: guestView('live').state } });
  let liveAnswers = 0;
  await page.route('**/api/decks/*/live', async (route) => {
    if (!canPresent) {
      await route.fulfill({ status: 403, json: { error: 'forbidden' } });
      liveAnswers += 1;
      return;
    }
    await route.fulfill({
      json: {
        sessionId: 'session-1',
        code: liveJoin.code,
        codeLabel: liveJoin.codeLabel,
        joinUrl: liveJoin.joinUrl,
        hostLabel: liveJoin.hostLabel,
        live: true,
        joinedCount: 3,
        joinBar: false,
      },
    });
    liveAnswers += 1;
  });
  const reads: number[] = [];
  await page.route('**/api/decks/*/slides/*/response', (route) => {
    reads.push(Number(new URL(route.request().url()).pathname.split('/')[5]));
    return route.fulfill({ json: view });
  });
  return {
    hub,
    reads,
    get liveAnswers() {
      return liveAnswers;
    },
  };
}

// The player as a viewer the page takes for the owner; GET /live decides whether they can present.
function player() {
  return (
    <JoinProvider deckId={deckId} join={liveJoin} owner>
      <OpenResponseScreenLive deckId={deckId} owner>
        <SlideRenderer slide={cloudSlide} deck={deck} total={6} step={0} />
      </OpenResponseScreenLive>
    </JoinProvider>
  );
}

const slide = (page: Page) => page.getByTestId(`slide-${SLIDE}`);
const word = (page: Page, index: number) => slide(page).getByTestId(`word-cloud-word-${index}`);
// Lets the page run its pending work, so a call or render it should not make has had its chance.
const settle = (page: Page) => page.evaluate(() => new Promise((done) => setTimeout(done, 300)));
const fontPx = (page: Page, index: number) =>
  word(page, index).evaluate((element) => parseFloat(getComputedStyle(element).fontSize));

test('WordCloudLayout_WhenOneWordHasAHigherCount_ShouldRenderItLarger', async ({ mount, page }) => {
  await stubLive(page, control(5, [
    { number: 1, text: 'Calm', count: 2, hidden: false },
    { number: 2, text: 'Busy', count: 1, hidden: false },
  ]));
  await mount(player());

  await expect(word(page, 0)).toHaveText('Calm');
  await expect(word(page, 1)).toHaveText('Busy');
  expect(await fontPx(page, 0)).toBeGreaterThan(await fontPx(page, 1));
});

test('WordCloudLayout_WhenAWordIsHidden_ShouldLeaveItOutAndKeepTheQuestion', async ({ mount, page }) => {
  const { hub } = await stubLive(page, control(5, [{ number: 1, text: 'Calm', count: 1, hidden: false }]));
  await mount(player());
  await expect(word(page, 0)).toHaveText('Calm');

  const hidden: WordCloudEvent = {
    type: 'wordCloud',
    slide: SLIDE,
    phase: 'open',
    round: ROUND,
    number: 1,
    revision: 6,
    revealed: true,
    guests: 2,
    words: [
      { text: 'Rubbish', count: 3, hidden: true },
      { text: 'Busy', count: 2, hidden: false },
      { text: 'Calm', count: 1, hidden: false },
    ],
  };
  hub.pushWordCloud(hidden, { target: 'screen' });

  await expect(word(page, 0)).toHaveText('Busy');
  await expect(word(page, 1)).toHaveText('Calm');
  await expect(word(page, 2)).toHaveCount(0);
  await expect(slide(page).getByText('Rubbish')).toHaveCount(0);
  await expect(slide(page).getByText(QUESTION)).toBeVisible();
});

test('Cloud_WhenTheViewerCannotPresent_ShouldShowTheQuestionOnly', async ({ mount, page }) => {
  const live = await stubLive(page, control(5, [{ number: 1, text: 'Calm', count: 4, hidden: false }]), {
    canPresent: false,
  });
  await mount(player());
  await expect.poll(() => live.liveAnswers).toBe(1);
  await expect.poll(() => live.hub.sockets).toBeGreaterThanOrEqual(1);
  await settle(page);

  expect(live.hub.watches).toEqual([]);
  expect(live.reads).toEqual([]);
  await expect(slide(page).getByText(QUESTION)).toBeVisible();
  await expect(slide(page).getByRole('list')).toHaveCount(0);
  await expect(word(page, 0)).toHaveCount(0);
});

test('WordCloudLayout_WhenOneWordIsShownScreenshot_ShouldMatchTheBaseline', async ({ mount, page }) => {
  await stubLive(page, control(5, [{ number: 1, text: 'Focused', count: 3, hidden: false }]));
  await mount(player());
  await expect(word(page, 0)).toHaveText('Focused');

  await expect(slide(page)).toHaveScreenshot('word-cloud-one-word.png');
});
