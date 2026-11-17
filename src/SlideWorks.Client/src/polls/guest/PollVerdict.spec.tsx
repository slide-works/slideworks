import { test, expect } from '@playwright/experimental-ct-react';
import { pollActivity, pollTally } from '../../test-support/fixtures/pollStates';
import { POLL_CARD_TEST_IDS } from '../testIds';
import { PollAnswerFixture } from './PollAnswerFixture';
import { PollCard } from './PollCard';

// After the presenter shows the answer, on a 360 px phone: whether the guest got it (D6), through the card.
const shown = pollActivity('closed', { correct: 1 });
const TALLY = pollTally({ counts: [22, 9, 5, 4], answered: 40 });
const T = POLL_CARD_TEST_IDS;

test.beforeEach(async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 740 });
});

test('right: you got it', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture picks={[0]} mine={{ picks: [0], revision: 1 }} tally={TALLY}>
      <PollCard activity={shown} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.verdictRight)).toHaveText('You got it.');
  await expect(page.getByTestId(T.verdictMiss)).toHaveCount(0);
  await expect(page.getByTestId(T.verdict)).toHaveRole('status');
});

test('wrong: not this time, and the answer named', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture picks={[1]} mine={{ picks: [1], revision: 1 }} tally={TALLY}>
      <PollCard activity={shown} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.verdictMiss)).toHaveText(
    'Not this time. The answer was “Under 50 lines”.',
  );
  await expect(page.getByTestId(T.verdictRight)).toHaveCount(0);
});

test('no answer: only the answer is named, and the foot says none was sent', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture tally={TALLY}>
      <PollCard activity={shown} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.verdict)).toHaveText('The answer was “Under 50 lines”.');
  await expect(page.getByTestId(T.verdictRight)).toHaveCount(0);
  await expect(page.getByTestId(T.verdictMiss)).toHaveCount(0);
  await expect(page.getByTestId(T.noAnswer)).toBeVisible();
});

test('pick-one answer shown, you got it, screenshot at 360', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture picks={[0]} mine={{ picks: [0], revision: 1 }} tally={TALLY}>
      <PollCard activity={shown} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.card)).toHaveScreenshot('poll-pick-one-got-it-360.png');
});
