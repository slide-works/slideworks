import { test, expect } from '@playwright/experimental-ct-react';
import { pollActivity } from '../../test-support/fixtures/pollStates';
import { POLL_CARD_TEST_IDS, pollOptionTestId } from '../testIds';
import { PollAnswerFixture } from './PollAnswerFixture';
import { PollCard } from './PollCard';
import { PollQueued } from './PollQueued';

// The dropped-connection card on a 360 px phone: an unsent pick waits on the phone (V6).
test.beforeEach(async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 740 });
});

test('shows "Not sent yet" and says the pick goes by itself', async ({ mount, page }) => {
  await mount(<PollQueued />);
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.queuedMessage)).toHaveText('Not sent yet');
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.queued)).toContainText(
    'sends by itself when the connection is back',
  );
});

test('in the card there is no Send button, no Change button, and the pick stays ticked', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture state="queued" picks={[0, 3]} pick="upTo" max={2}>
      <PollCard activity={pollActivity('open', { pick: 'upTo', max: 2 })} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.queuedMessage)).toHaveText('Not sent yet');
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.send)).toHaveCount(0);
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.change)).toHaveCount(0);
  await expect(page.getByRole('button')).toHaveCount(0);
  await expect(page.getByTestId(pollOptionTestId(0))).toBeChecked();
  await expect(page.getByTestId(pollOptionTestId(3))).toBeChecked();
});

test('the dropped connection screenshot at 360', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture state="queued" picks={[1]}>
      <PollCard activity={pollActivity('open')} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page).toHaveScreenshot('queued.png', { maxDiffPixels: 100 });
});
