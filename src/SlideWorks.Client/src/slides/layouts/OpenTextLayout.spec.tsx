import { test, expect } from '@playwright/experimental-ct-react';
import type { ReactElement } from 'react';
import { OpenResponseScreenLive } from '../../openResponses/OpenResponseScreenLive';
import type { OpenTextEvent, WallAnswer } from '../../openResponses/types';
import { guestView } from '../../test-support/fixtures/guestStates';
import { LAYOUT_SLOTS } from '../../test-support/fixtures/layoutSlots';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { deckPlay } from '../../test-support/fixtures/screens';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { JoinProvider } from '../JoinContext';
import { SlideRenderer } from '../SlideRenderer';
import type { SlideDeck } from '../slots';

test.use({ viewport: { width: 1920, height: 1080 } });

const SLIDE = 3;
const join = { ...deckPlay.join, live: true };
const deck: SlideDeck = {
  title: deckPlay.title,
  author: deckPlay.author,
  parts: [],
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function eventOf(answers: WallAnswer[]): OpenTextEvent {
  return {
    type: 'openText',
    slide: SLIDE,
    phase: 'open',
    round: 'a3d81c5e-7f2b-4e96-b0c4-1d8e6f9a2b57',
    number: 1,
    revision: 20,
    revealed: true,
    guests: answers.length,
    answers,
  };
}

async function show(
  page: Parameters<typeof stubDeckHub>[0],
  mount: (node: ReactElement) => Promise<unknown>,
  answers: WallAnswer[],
) {
  const hub = await stubDeckHub(page, { audience: { state: guestView('live').state } });
  await page.route('**/api/decks/*/live', (route) =>
    route.fulfill({
      json: {
        sessionId: 'session-1',
        code: join.code,
        codeLabel: join.codeLabel,
        joinUrl: join.joinUrl,
        hostLabel: join.hostLabel,
        live: true,
        joinedCount: answers.length,
        joinBar: false,
        readingIn: null,
      },
    }),
  );
  await mount(
    <JoinProvider deckId={deckPlay.id} join={join} owner>
      <OpenResponseScreenLive deckId={deckPlay.id} owner>
        <SlideRenderer
          slide={{
            number: SLIDE,
            layout: 'open-text',
            part: null,
            deepDive: false,
            slots: LAYOUT_SLOTS['open-text'],
          }}
          deck={deck}
          total={8}
          step={0}
        />
      </OpenResponseScreenLive>
    </JoinProvider>,
  );
  await expect.poll(() => hub.watches.length).toBeGreaterThanOrEqual(2);
  hub.pushAudienceEvent(eventOf(answers));
}

test('OpenTextLayout_WhenAnswersArriveOutOfNumberOrder_ShouldShowNumberOrder', async ({
  mount,
  page,
}) => {
  await show(page, mount, [
    { number: 2, text: 'Apple second', hidden: false },
    { number: 1, text: 'Zebra first', hidden: false },
  ]);

  await expect(page.getByTestId('open-text-headline')).toHaveText(
    LAYOUT_SLOTS['open-text'].question,
  );
  await expect(page.getByTestId('open-text-text')).toHaveText(['Zebra first', 'Apple second']);
  await expect(page.getByTestId('open-text-number')).toHaveText(['1', '2']);
});

test('OpenTextLayout_WhenOneAnswerIsShownScreenshot_ShouldMatchTheBaseline', async ({
  mount,
  page,
}) => {
  await show(page, mount, [
    { number: 1, text: 'Smaller stories, so review is quick', hidden: false },
  ]);

  await expect(page.getByTestId('open-text-text')).toHaveText(
    'Smaller stories, so review is quick',
  );
  await expect(page.getByTestId('slide-frame')).toHaveScreenshot('open-text-one-answer.png');
});
