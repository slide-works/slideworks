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

test('the status region is mounted empty and filled after, so it is announced; its text is dark enough', async ({
  mount,
  page,
}) => {
  await page.evaluate(() => {
    const seen: string[] = [];
    (window as unknown as { seen: string[] }).seen = seen;
    new MutationObserver(() => {
      const el = document.querySelector('[data-testid="poll-queued-message"]');
      if (el) seen.push(el.textContent ?? '');
    }).observe(document, { childList: true, subtree: true, characterData: true });
  });
  await mount(<PollQueued />);
  const message = page.getByTestId(POLL_CARD_TEST_IDS.queuedMessage);
  await expect(message).toHaveAttribute('role', 'status');
  await expect(message).toHaveText('Not sent yet');
  const seen = await page.evaluate(() => (window as unknown as { seen: string[] }).seen);
  expect(seen[0]).toBe('');
  expect(seen.at(-1)).toBe('Not sent yet');
  // The warning ink on the card's white, at least 4.5:1 for 13 px text.
  const ratio = await message.evaluate((el) => {
    const lum = (rgb: string) => {
      const [r, g, b] = (rgb.match(/[\d.]+/g) ?? [])
        .slice(0, 3)
        .map(Number)
        .map((v) => {
          const c = v / 255;
          return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
        });
      return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    };
    const fg = lum(getComputedStyle(el).color);
    const bg = lum('rgb(255, 255, 255)');
    return (bg + 0.05) / (fg + 0.05);
  });
  expect(ratio).toBeGreaterThanOrEqual(4.5);
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
