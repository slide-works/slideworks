import { test, expect } from '@playwright/experimental-ct-react';
import { Player } from '../../player/Player';
import { qaControlView } from '../../test-support/fixtures/qaStates';
import { QA_SLIDE_NUMBER, deckPlayWithQa } from '../../test-support/fixtures/screens';
import { mountPlayer } from '../../test-support/mountPlayer';
import { stubApi } from '../../test-support/stubApi';
import { QA_CARD_TEST_IDS as IDS } from '../testIds';
import type { QaControlView } from '../types';
import { QaScreenFixture } from './QaScreenFixture';

// The question card over the player's slides (SW-9 Q9; mockup screen state 1), through the fixture provider.
test.use({ viewport: { width: 1920, height: 1080 } });

test.beforeEach(async ({ page }) => {
  await stubApi(page, {});
});

async function playerAt(
  mount: Parameters<typeof mountPlayer>[0],
  page: Parameters<typeof mountPlayer>[1],
  slide: number,
  control: QaControlView | null,
) {
  await page.evaluate((hash) => window.history.replaceState(null, '', hash), `#${slide}`);
  await mountPlayer(
    mount,
    page,
    <QaScreenFixture control={control}>
      <Player deck={deckPlayWithQa()} />
    </QaScreenFixture>,
  );
  await expect(page.getByTestId(`slide-${slide}`)).toBeVisible();
}

const live = qaControlView('live');

test('the card shows the shown question over a text slide', async ({ mount, page }) => {
  await playerAt(mount, page, 1, live);

  await expect(page.getByTestId(IDS.card)).toBeVisible();
  await expect(page.getByTestId(IDS.label)).toHaveText('Question from the room');
  await expect(page.getByTestId(IDS.votes)).toHaveText('▲ 9');
  await expect(page.getByTestId(IDS.name)).toHaveText('Anonymous');
  await expect(page.getByTestId(IDS.text)).toHaveText(
    'What is the smallest slice you have shipped?',
  );
  await expect(page.getByTestId('slide-1')).toBeVisible();
});

test('the card names the asker when the question has a name', async ({ mount, page }) => {
  const named = {
    ...live,
    questions: live.questions.map((q) =>
      q.number === live.onScreen ? { ...q, name: 'Priya' } : q,
    ),
  };
  await playerAt(mount, page, 1, named);

  await expect(page.getByTestId(IDS.name)).toHaveText('Priya');
});

test('no card on a qa slide, which draws the question itself', async ({ mount, page }) => {
  await playerAt(mount, page, QA_SLIDE_NUMBER, live);

  await expect(page.getByTestId(IDS.layer)).toHaveAttribute('data-qa-slide', 'true');
  await expect(page.getByTestId(IDS.card)).toHaveCount(0);
});

test('no card when the question is set to show on the qa slide only', async ({ mount, page }) => {
  await playerAt(mount, page, 1, { ...live, settings: { ...live.settings, showOn: 'qaSlide' } });

  await expect(page.getByTestId(IDS.layer)).toHaveAttribute('data-qa-slide', 'false');
  await expect(page.getByTestId(IDS.card)).toHaveCount(0);
});

test('no card when no question is on screen', async ({ mount, page }) => {
  await playerAt(mount, page, 1, { ...live, onScreen: null });

  await expect(page.getByTestId(IDS.layer)).toBeAttached();
  await expect(page.getByTestId(IDS.card)).toHaveCount(0);
});

test('no card when the question on screen is not in the list', async ({ mount, page }) => {
  await playerAt(mount, page, 1, { ...live, onScreen: 99 });

  await expect(page.getByTestId(IDS.card)).toHaveCount(0);
});

test('no layer and no card for a screen with no live data', async ({ mount, page }) => {
  await playerAt(mount, page, 1, null);

  await expect(page.getByTestId(IDS.layer)).toHaveCount(0);
  await expect(page.getByTestId(IDS.card)).toHaveCount(0);
});

test('the card is gone when the deck is not live', async ({ mount, page }) => {
  await playerAt(mount, page, 1, qaControlView('notLive'));

  await expect(page.getByTestId(IDS.layer)).toHaveCount(0);
});

test('the card renders question text as text, not HTML', async ({ mount, page }) => {
  const hostile = {
    ...live,
    questions: live.questions.map((q) =>
      q.number === live.onScreen ? { ...q, text: '<img src=x onerror=alert(1)>' } : q,
    ),
  };
  await playerAt(mount, page, 1, hostile);

  await expect(page.getByTestId(IDS.text)).toHaveText('<img src=x onerror=alert(1)>');
  await expect(page.getByTestId(IDS.card).locator('img')).toHaveCount(0);
});

test('screenshot the card over a text slide', async ({ mount, page }) => {
  await playerAt(mount, page, 1, live);

  await expect(page.getByTestId('player-canvas')).toHaveScreenshot('question-card.png', {
    maxDiffPixels: 200,
  });
});
