import { test, expect } from '@playwright/experimental-ct-react';
import { pollActivity } from '../../test-support/fixtures/pollStates';
import { POLL_CARD_TEST_IDS, pollOptionTestId } from '../testIds';
import { PollAnswerFixture } from './PollAnswerFixture';
import { PollCard } from './PollCard';
import { PollConflict } from './PollConflict';

// The changed-poll card on a 360 px phone: a 409 came back, the pick is kept and the card asks again (V6).
const QUESTION = 'How big was your last merge request?';

test.beforeEach(async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 740 });
});

test('shows the message with focus on it, once: focus without an alert role', async ({
  mount,
  page,
}) => {
  await mount(
    <PollConflict
      activity={pollActivity('open', { number: 2 })}
      presenter="Sam"
      onSend={() => {}}
      onClear={() => {}}
    />,
  );
  const message = page.getByTestId(POLL_CARD_TEST_IDS.conflictMessage);
  await expect(message).toHaveText('Sam changed the poll. Send this pick again?');
  await expect(message).toBeFocused();
  await expect(message).not.toHaveAttribute('role', 'alert');
});

test('it never claims a new round, in round 1 or later: a change within the round reads the same', async ({
  mount,
  page,
}) => {
  const card = await mount(
    <PollConflict
      activity={pollActivity('open', { number: 1 })}
      presenter="Sam"
      onSend={() => {}}
      onClear={() => {}}
    />,
  );
  const message = page.getByTestId(POLL_CARD_TEST_IDS.conflictMessage);
  await expect(message).toHaveText('Sam changed the poll. Send this pick again?');
  await card.update(
    <PollConflict
      activity={pollActivity('open', { number: 3 })}
      presenter="Sam"
      onSend={() => {}}
      onClear={() => {}}
    />,
  );
  await expect(message).not.toContainText('round');
});

test('Send it calls send once and Clear calls clear once, each named with the question', async ({
  mount,
  page,
}) => {
  let sends = 0;
  let clears = 0;
  await mount(
    <PollConflict
      activity={pollActivity('open')}
      presenter="Sam"
      onSend={() => (sends += 1)}
      onClear={() => (clears += 1)}
    />,
  );
  const send = page.getByRole('button', { name: `Send it: ${QUESTION}` });
  const clear = page.getByRole('button', { name: `Clear: ${QUESTION}` });
  await expect(send).toHaveText('Send it');
  expect((await send.boundingBox())!.height).toBeGreaterThanOrEqual(44);
  expect((await clear.boundingBox())!.height).toBeGreaterThanOrEqual(44);
  await send.click();
  await expect.poll(() => sends).toBe(1);
  expect(clears).toBe(0);
  await clear.click();
  await expect.poll(() => clears).toBe(1);
  expect(sends).toBe(1);
});

test('in the card the pick is kept ticked, the foot gives way, and focus moves to the message', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture state="conflict" picks={[1, 2]} pick="upTo" max={2}>
      <PollCard activity={pollActivity('open', { pick: 'upTo', max: 2 })} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.conflict)).toBeVisible();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.conflictMessage)).toBeFocused();
  await expect(page.getByTestId(pollOptionTestId(1))).toBeChecked();
  await expect(page.getByTestId(pollOptionTestId(2))).toBeChecked();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.send)).toHaveCount(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
});

test('the changed poll screenshot at 360', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture state="conflict" picks={[2]}>
      <PollCard activity={pollActivity('open', { number: 2 })} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page).toHaveScreenshot('conflict.png', { maxDiffPixels: 100 });
});
