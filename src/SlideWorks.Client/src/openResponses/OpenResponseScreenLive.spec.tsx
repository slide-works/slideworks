import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { DeckPlaySlide } from '../api/types';
import { JoinProvider } from '../slides/JoinContext';
import { SlideRenderer } from '../slides/SlideRenderer';
import type { SlideDeck } from '../slides/slots';
import { guestView } from '../test-support/fixtures/guestStates';
import { LAYOUT_SLOTS } from '../test-support/fixtures/layoutSlots';
import { lookViewOf } from '../test-support/fixtures/lookViewOf';
import { deckPlay } from '../test-support/fixtures/screens';
import { stubDeckHub } from '../test-support/stubDeckHub';
import { OpenResponseScreenLive } from './OpenResponseScreenLive';
import type { OpenResponseControlView } from './types';
import { settle } from '../test-support/gate';

test.use({ viewport: { width: 1920, height: 1080 } });

// The real screen provider over the stub hub and a stubbed GET /api/decks/{id}/slides/{slide}/response, with the
// word-cloud layout as the slides that mark themselves. Slides 2 and 3 of the fixture deck are word clouds.
const deckId = deckPlay.id;
const liveJoin = { ...deckPlay.join, live: true };
const ROUND = '6e2f9a14-3c7b-4d58-a1e0-9b4c7d2f5a63';
const cloudSlide = (number: number): DeckPlaySlide =>
  ({
    ...deckPlay.slides[number - 1],
    layout: 'word-cloud',
    steps: 0,
    slots: structuredClone(LAYOUT_SLOTS['word-cloud']),
  }) as DeckPlaySlide;
const SLIDES = [cloudSlide(2), cloudSlide(3)];
const deck: SlideDeck = {
  title: deckPlay.title,
  author: deckPlay.author,
  parts: [],
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

// Each slide's control view: one word, named for the slide, so a read for one slide can't pass for the other's.
const viewOf = (slide: number): OpenResponseControlView => ({
  kind: 'word-cloud',
  phase: 'open',
  round: ROUND,
  number: 1,
  revision: 5,
  results: 'live',
  opens: 'arrival',
  revealed: true,
  guests: 1,
  entries: [{ number: 1, text: `Slide${slide}`, count: 1, hidden: false }],
});

// The deck is live and GET /live confirms the viewer. Each GET of a slide's view is recorded by its slide number.
async function stubLive(page: Page) {
  const hub = await stubDeckHub(page, { audience: { state: guestView('live').state } });
  await page.route('**/api/decks/*/live', (route) =>
    route.fulfill({
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
    }),
  );
  const reads: number[] = [];
  await page.route('**/api/decks/*/slides/*/response', (route) => {
    const slide = Number(new URL(route.request().url()).pathname.split('/')[5]);
    reads.push(slide);
    return route.fulfill({ json: viewOf(slide) });
  });
  return { hub, reads };
}

// The live session always takes the viewer for the owner (GET /live confirms them); `owner` is the flag the screen
// provider itself gets.
function player(owner: boolean) {
  return (
    <JoinProvider deckId={deckId} join={liveJoin} owner>
      <OpenResponseScreenLive deckId={deckId} owner={owner}>
        {SLIDES.map((slide) => (
          <SlideRenderer key={slide.number} slide={slide} deck={deck} total={6} step={0} />
        ))}
      </OpenResponseScreenLive>
    </JoinProvider>
  );
}

// Lets the page run its pending work, so a call or render it should not make has had its chance.
const word = (page: Page, slide: number, index: number) =>
  page.getByTestId(`slide-${slide}`).getByTestId(`word-cloud-word-${index}`);

test('OpenResponseScreenLive_WhenThePresenterIsLive_ShouldWatchTheAudience', async ({
  mount,
  page,
}) => {
  const { hub } = await stubLive(page);
  await mount(player(true));

  // One watch is the live session's; the other is the screen provider's own.
  await expect.poll(() => hub.watches.filter((id) => id === deckId)).toHaveLength(2);
  await expect(word(page, 2, 0)).toHaveText('Slide2');
});

test('OpenResponseScreenLive_WhenTwoSlidesAreMarked_ShouldReadEachSlideOnce', async ({
  mount,
  page,
}) => {
  const { reads } = await stubLive(page);
  await mount(player(true));

  await expect(word(page, 2, 0)).toHaveText('Slide2');
  await expect(word(page, 3, 0)).toHaveText('Slide3');
  expect([...reads].sort((a, b) => a - b)).toEqual([2, 3]);
});

test('OpenResponseScreenLive_WhenAnEventHasAHiddenWord_ShouldDropIt', async ({ mount, page }) => {
  const { hub } = await stubLive(page);
  await mount(player(true));
  await expect(word(page, 3, 0)).toHaveText('Slide3');

  hub.pushWordCloud(
    {
      type: 'wordCloud',
      slide: 3,
      phase: 'open',
      round: ROUND,
      number: 1,
      revision: 6,
      revealed: true,
      guests: 2,
      words: [
        { text: 'Rubbish', count: 4, hidden: true },
        { text: 'Calm', count: 2, hidden: false },
      ],
    },
    { target: 'screen' },
  );

  await expect(word(page, 3, 0)).toHaveText('Calm');
  await expect(word(page, 3, 1)).toHaveCount(0);
  await expect(page.getByText('Rubbish')).toHaveCount(0);
  await expect(word(page, 2, 0)).toHaveText('Slide2');
});

test('OpenResponseScreenLive_WhenTheViewerIsNotTheOwner_ShouldGiveNoRound', async ({
  mount,
  page,
}) => {
  const { hub, reads } = await stubLive(page);
  await mount(player(false));
  await expect.poll(() => hub.watches).toEqual([deckId]);

  hub.pushWordCloud(
    {
      type: 'wordCloud',
      slide: 2,
      phase: 'open',
      round: ROUND,
      number: 1,
      revision: 6,
      revealed: true,
      guests: 1,
      words: [{ text: 'Calm', count: 1, hidden: false }],
    },
    { target: 'screen' },
  );
  await settle(page, 300);

  expect(hub.watches).toEqual([deckId]);
  expect(reads).toEqual([]);
  await expect(page.getByTestId('slide-2').getByTestId('layout-word-cloud')).toBeVisible();
  await expect(page.getByTestId('slide-3').getByTestId('layout-word-cloud')).toBeVisible();
  await expect(word(page, 2, 0)).toHaveCount(0);
  await expect(word(page, 3, 0)).toHaveCount(0);
});
