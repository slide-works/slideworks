import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { Player } from '../../player/Player';
import { SlideRenderer } from '../../slides/SlideRenderer';
import type { SlideDeck } from '../../slides/slots';
import { LAYOUT_SLOTS } from '../../test-support/fixtures/layoutSlots';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { qaControlView } from '../../test-support/fixtures/qaStates';
import { QA_SLIDE_NUMBER, deckPlayWithQa } from '../../test-support/fixtures/screens';
import { mountPlayer } from '../../test-support/mountPlayer';
import { QaScreenProbe } from '../../test-support/QaScreenProbe';
import { stubApi } from '../../test-support/stubApi';
import { QA_CARD_TEST_IDS, QA_REACTIONS_LAYER_TEST_IDS, QA_SLIDE_TEST_IDS } from '../testIds';
import type { QaScreen } from './QaScreenContext';
import { QaScreenFixture } from './QaScreenFixture';
import { ScreenReactionsFixture } from './ScreenReactionsFixture';
import type { ScreenReactions } from './useScreenReactions';

// The big screen's seams (SW-9 Q9): the qa slide, the two hooks through their fixture providers, and the two layers
// mounted over every slide of the player. Stubs draw nothing yet, so these assert root test ids and hook values only.

const deck: SlideDeck = {
  title: 'Ship smaller',
  author: 'Sam Rivera',
  parts: [],
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

const qaSlide = (
  <SlideRenderer
    slide={{ number: 6, layout: 'qa', part: null, deepDive: false, slots: LAYOUT_SLOTS.qa }}
    deck={deck}
    total={6}
    step={0}
  />
);

const probed = async <T,>(page: Page, testId: string): Promise<T> =>
  JSON.parse((await page.getByTestId(testId).textContent()) ?? '') as T;

const qaScreen = (page: Page) => probed<QaScreen | null>(page, 'probe-qa-screen');
const reactions = (page: Page) => probed<ScreenReactions | null>(page, 'probe-screen-reactions');

test.beforeEach(async ({ page }) => {
  await stubApi(page, {});
});

test('a qa slide renders its root and its not-live state with a null screen', async ({
  mount,
  page,
}) => {
  await mount(
    <QaScreenFixture control={null}>
      {qaSlide}
      <QaScreenProbe />
    </QaScreenFixture>,
  );

  await expect(page.getByTestId('layout-qa')).toBeVisible();
  await expect(page.getByTestId(QA_SLIDE_TEST_IDS.slide)).toBeAttached();
  await expect(page.getByTestId(QA_SLIDE_TEST_IDS.notLive)).toBeAttached();
  expect(await qaScreen(page)).toBeNull();
});

test('a qa slide outside any provider renders its root with a null screen', async ({
  mount,
  page,
}) => {
  await mount(
    <div>
      {qaSlide}
      <QaScreenProbe />
    </div>,
  );

  await expect(page.getByTestId(QA_SLIDE_TEST_IDS.slide)).toBeAttached();
  await expect(page.getByTestId(QA_SLIDE_TEST_IDS.notLive)).toBeAttached();
  expect(await qaScreen(page)).toBeNull();
  expect(await reactions(page)).toBeNull();
});

test('the fixture drives a list: the open questions, most votes first, none up', async ({
  mount,
  page,
}) => {
  await mount(
    <QaScreenFixture control={qaControlView('live', { onScreen: null })}>
      {qaSlide}
      <QaScreenProbe />
    </QaScreenFixture>,
  );

  const screen = await qaScreen(page);
  expect(screen).toMatchObject({ phase: 'open', total: 3, onScreen: null, showOn: 'anySlide' });
  expect(screen?.top.map((row) => [row.number, row.votes])).toEqual([
    [1, 14],
    [2, 9],
    [6, 3],
  ]);
  await expect(page.getByTestId(QA_SLIDE_TEST_IDS.slide)).toBeAttached();
  await expect(page.getByTestId(QA_SLIDE_TEST_IDS.notLive)).toHaveCount(0);
});

test('the fixture drives one question up', async ({ mount, page }) => {
  await mount(
    <QaScreenFixture control={qaControlView('live')}>
      {qaSlide}
      <QaScreenProbe />
    </QaScreenFixture>,
  );

  expect((await qaScreen(page))?.onScreen).toEqual({
    number: 2,
    text: 'What is the smallest slice you have shipped?',
    name: null,
    votes: 9,
  });
});

test('the fixture adds a burst', async ({ mount, page }) => {
  const component = await mount(
    <ScreenReactionsFixture bursts={[]}>
      <QaScreenProbe />
    </ScreenReactionsFixture>,
  );
  expect(await reactions(page)).toEqual({ bursts: [] });

  await component.update(
    <ScreenReactionsFixture bursts={[{ id: 1, emoji: 'clap' }]}>
      <QaScreenProbe />
    </ScreenReactionsFixture>,
  );

  await expect(page.getByTestId('probe-screen-reactions')).toHaveText(
    JSON.stringify({ bursts: [{ id: 1, emoji: 'clap' }] }),
  );
});

test('the layers add nothing to the player when their hooks are null', async ({ mount, page }) => {
  await mountPlayer(
    mount,
    page,
    <QaScreenFixture control={null}>
      <ScreenReactionsFixture bursts={null}>
        <Player deck={deckPlayWithQa()} />
      </ScreenReactionsFixture>
    </QaScreenFixture>,
  );

  await expect(page.getByTestId('slide-1')).toBeVisible();
  await expect(page.getByTestId(QA_CARD_TEST_IDS.layer)).toHaveCount(0);
  await expect(page.getByTestId(QA_REACTIONS_LAYER_TEST_IDS.layer)).toHaveCount(0);
});

test('the layers mount once over the player canvas when their hooks give values', async ({
  mount,
  page,
}) => {
  await page.evaluate((hash) => window.history.replaceState(null, '', hash), `#${QA_SLIDE_NUMBER}`);
  await mountPlayer(
    mount,
    page,
    <QaScreenFixture control={qaControlView('live')}>
      <ScreenReactionsFixture bursts={[{ id: 1, emoji: 'heart' }]}>
        <Player deck={deckPlayWithQa()} />
      </ScreenReactionsFixture>
    </QaScreenFixture>,
  );

  const canvas = page.getByTestId('player-canvas');
  await expect(page.getByTestId(`slide-${QA_SLIDE_NUMBER}`)).toBeVisible();
  await expect(canvas.getByTestId(QA_CARD_TEST_IDS.layer)).toHaveCount(1);
  await expect(canvas.getByTestId(QA_REACTIONS_LAYER_TEST_IDS.layer)).toHaveCount(1);
});
