import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AppRoutes } from '../../AppRoutes';
import { SlideThumb } from '../../pages/deck/SlideThumb';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { pollControlView } from '../../test-support/fixtures/pollStates';
import {
  POLL_SLIDE_NUMBER,
  deckPageWithPoll,
  deckPlayWithPoll,
} from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import type { SlideDeck } from '../../slides/slots';
import {
  POLL_PANEL_TEST_IDS,
  POLL_SLIDE_TEST_IDS,
  POLL_STRIP_TEST_IDS,
  pollSlideRowTestId,
} from '../testIds';
import { PollControlFixture } from './PollControlFixture';
import { PollPanel } from './PollPanel';
import { PollStrip } from './PollStrip';

// SW-6 wave 0.1's seams in the browser: the poll slide in the player page and as a deck-page thumbnail, the panel and
// strip stubs under the control fixture. The poll control routes' stub has its own spec (stubPollControlApi.spec.tsx).

const QUESTION = 'How big was your last merge request?';
const OPTIONS = ['Under 50 lines', '50 to 200 lines', '200 to 1,000 lines', 'Over 1,000 lines'];

async function expectPollSlide(slide: ReturnType<Page['getByTestId']>) {
  const poll = slide.getByTestId(POLL_SLIDE_TEST_IDS.slide);
  await expect(poll).toHaveCount(1);
  await expect(poll.getByTestId(POLL_SLIDE_TEST_IDS.question)).toHaveText(QUESTION);
  for (const [position, option] of OPTIONS.entries())
    await expect(poll.getByTestId(pollSlideRowTestId(position))).toHaveText(option);
}

test('the player page draws a poll slide with its question and options', async ({
  mount,
  page,
}) => {
  const deck = deckPlayWithPoll();
  await stubApi(page, { [`/api/play/${deck.id}`]: { json: deck } });
  await page.evaluate(
    (hash) => window.history.replaceState(window.history.state, '', hash),
    `#${POLL_SLIDE_NUMBER}`,
  );
  await mount(<AppRoutes />, { hooksConfig: { route: `/d/${deck.id}/${deck.slug}` } });

  const slide = page.getByTestId(`slide-${POLL_SLIDE_NUMBER}`);
  await expect(slide).toBeVisible();
  await expect(slide).toHaveAttribute('data-layout', 'poll');
  await expectPollSlide(slide);
});

test('a deck-page thumbnail draws a poll slide with its question and options', async ({
  mount,
  page,
}) => {
  const view = deckPageWithPoll();
  const deck: SlideDeck = {
    title: view.title,
    author: null,
    parts: [],
    look: lookViewOf('talk', 'cobalt'),
    deepDives: [],
  };
  const slide = view.slides.find((s) => s.number === POLL_SLIDE_NUMBER)!;
  await mount(
    <ul>
      <SlideThumb
        slide={slide}
        deck={deck}
        total={view.slides.length}
        selected={false}
        onSelect={() => {}}
      />
    </ul>,
  );
  await expectPollSlide(page.getByTestId('deck-thumb'));
});

test('the panel stub shows the phase from the control fixture, and says when there is no poll', async ({
  mount,
  page,
}) => {
  const panel = await mount(
    <PollControlFixture deckId="deck-1" control={pollControlView('closed')}>
      <PollPanel deckId="deck-1" />
    </PollControlFixture>,
  );
  await expect(page.getByTestId(POLL_PANEL_TEST_IDS.state)).toHaveText('closed');
  await panel.update(
    <PollControlFixture deckId="deck-1" control={null}>
      <PollPanel deckId="deck-1" />
    </PollControlFixture>,
  );
  await expect(page.getByTestId(POLL_PANEL_TEST_IDS.empty)).toBeVisible();
  await expect(page.getByTestId(POLL_PANEL_TEST_IDS.state)).toHaveCount(0);
});

test('the strip stub shows the phase from the control fixture, and nothing when there is no poll', async ({
  mount,
  page,
}) => {
  const strip = await mount(
    <PollControlFixture deckId="deck-1" control={pollControlView('open')}>
      <PollStrip deckId="deck-1" />
    </PollControlFixture>,
  );
  await expect(page.getByTestId(POLL_STRIP_TEST_IDS.state)).toHaveText('open');
  await strip.update(
    <PollControlFixture deckId="deck-1" control={null}>
      <PollStrip deckId="deck-1" />
    </PollControlFixture>,
  );
  await expect(page.getByTestId(POLL_STRIP_TEST_IDS.strip)).toHaveCount(0);
});
