import { test, expect } from '@playwright/experimental-ct-react';
import { pollActivity, pollTally } from '../../test-support/fixtures/pollStates';
import {
  POLL_CARD_TEST_IDS,
  pollOptionBarTestId,
  pollOptionCorrectTestId,
  pollOptionPercentTestId,
  pollOptionTestId,
  pollOptionYouTestId,
} from '../testIds';
import type { PollActivity } from '../types';
import { PollAnswerFixture } from './PollAnswerFixture';
import { PollCard } from './PollCard';

// The pick-one options on a 360 px phone, through the card and the fixture provider (V6, D3).
const one = (overrides: Partial<PollActivity> = {}) =>
  pollActivity('open', {
    pick: 'one',
    max: null,
    results: 'live',
    roomSeesCounts: true,
    ...overrides,
  });
const QUESTION = 'How big was your last merge request?';
const LIVE_TALLY = pollTally({ counts: [7, 17, 10, 4], answered: 38 });

test.beforeEach(async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 740 });
});

test('disabled reaches the radios: loading, not open yet and closed; never while open or sending', async ({
  mount,
  page,
}) => {
  const radios = page.getByRole('radio');
  const card = await mount(
    <PollAnswerFixture state="loading">
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(radios).toHaveCount(4);
  for (const radio of await radios.all()) await expect(radio).toBeDisabled();

  await card.update(
    <PollAnswerFixture>
      <PollCard activity={pollActivity('draft')} presenter="Sam" />
    </PollAnswerFixture>,
  );
  for (const radio of await radios.all()) await expect(radio).toBeDisabled();

  await card.update(
    <PollAnswerFixture picks={[1]} mine={{ picks: [1], revision: 1 }}>
      <PollCard activity={pollActivity('closed')} presenter="Sam" />
    </PollAnswerFixture>,
  );
  for (const radio of await radios.all()) await expect(radio).toBeDisabled();

  await card.update(
    <PollAnswerFixture>
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  for (const radio of await radios.all()) await expect(radio).toBeEnabled();

  await card.update(
    <PollAnswerFixture state="sending" picks={[2]}>
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  for (const radio of await radios.all()) await expect(radio).toBeEnabled();
});

test('a tap calls toggle once, sends, and focus stays on the option tapped', async ({
  mount,
  page,
}) => {
  const toggles: number[] = [];
  let sends = 0;
  await mount(
    <PollAnswerFixture onToggle={(i) => toggles.push(i)} onSend={() => (sends += 1)}>
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>,
  );

  await page.getByTestId(pollOptionTestId(1)).click();
  await expect(page.getByTestId(pollOptionTestId(1))).toBeChecked();
  await expect(page.getByTestId(pollOptionTestId(1))).toBeFocused();
  await expect.poll(() => toggles).toEqual([1]);
  await expect.poll(() => sends).toBe(1);

  // A tap anywhere on the option's row is a tap on its radio.
  await page.getByText('200 to 1,000 lines').click();
  await expect(page.getByTestId(pollOptionTestId(2))).toBeChecked();
  await expect(page.getByTestId(pollOptionTestId(1))).not.toBeChecked();
  await expect(page.getByTestId(pollOptionTestId(2))).toBeFocused();
  await expect.poll(() => toggles).toEqual([1, 2]);
  await expect.poll(() => sends).toBe(2);
});

test('real radios in a group labelled by the question, each named by its option and the question', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture>
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  const group = page.getByRole('radiogroup', { name: QUESTION });
  await expect(group.getByRole('radio')).toHaveCount(4);
  await expect(
    group.getByRole('radio', { name: `Under 50 lines ${QUESTION}`, exact: true }),
  ).toHaveAttribute('type', 'radio');
});

test('answered: the held pick is checked and marked You; a tap on another option changes it', async ({
  mount,
  page,
}) => {
  const toggles: number[] = [];
  await mount(
    <PollAnswerFixture
      picks={[1]}
      mine={{ picks: [1], revision: 1 }}
      tally={LIVE_TALLY}
      onToggle={(i) => toggles.push(i)}
    >
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(pollOptionTestId(1))).toBeChecked();
  await expect(page.getByTestId(pollOptionYouTestId(1))).toHaveText('You');
  await expect(page.getByTestId(pollOptionTestId(1))).toHaveAccessibleDescription(/You/);
  await expect(page.getByTestId(pollOptionYouTestId(0))).toHaveCount(0);

  await page.getByTestId(pollOptionTestId(3)).click();
  await expect.poll(() => toggles).toEqual([3]);
  await expect(page.getByTestId(pollOptionTestId(3))).toBeChecked();
  await expect(page.getByTestId(pollOptionTestId(3))).toBeFocused();
});

test('results never show before the guest has answered, even with live results and counts in the tally', async ({
  mount,
  page,
}) => {
  const card = await mount(
    <PollAnswerFixture tally={LIVE_TALLY}>
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.options)).toBeVisible();
  for (let i = 0; i < 4; i += 1) {
    await expect(page.getByTestId(pollOptionBarTestId(i))).toHaveCount(0);
    await expect(page.getByTestId(pollOptionPercentTestId(i))).toHaveCount(0);
  }
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.options)).not.toContainText('%');

  await card.update(
    <PollAnswerFixture picks={[1]} mine={{ picks: [1], revision: 1 }} tally={LIVE_TALLY}>
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(pollOptionPercentTestId(0))).toHaveText('18%');
  await expect(page.getByTestId(pollOptionPercentTestId(1))).toHaveText('45%');
  await expect(page.getByTestId(pollOptionPercentTestId(2))).toHaveText('26%');
  await expect(page.getByTestId(pollOptionPercentTestId(3))).toHaveText('11%');
  await expect(page.getByTestId(pollOptionBarTestId(1))).toHaveAttribute('style', 'width: 45%;');
});

test('bars hidden: an answered guest whose tally has no counts sees no bars', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture
      picks={[2]}
      mine={{ picks: [2], revision: 1 }}
      tally={pollTally({ counts: null, answered: 23 })}
    >
      <PollCard activity={one({ results: 'afterClose', roomSeesCounts: false })} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(pollOptionYouTestId(2))).toBeVisible();
  for (let i = 0; i < 4; i += 1)
    await expect(page.getByTestId(pollOptionBarTestId(i))).toHaveCount(0);
});

test('closed: the final bars with the guest’s answer marked, and nothing to tap', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture
      picks={[1]}
      mine={{ picks: [1], revision: 1 }}
      tally={pollTally({ counts: [8, 18, 11, 4], answered: 41 })}
    >
      <PollCard activity={pollActivity('closed')} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(pollOptionPercentTestId(1))).toHaveText('44%');
  await expect(page.getByTestId(pollOptionYouTestId(1))).toBeVisible();
  await expect(page.getByTestId(pollOptionTestId(1))).toBeChecked();
  await expect(page.getByTestId(pollOptionTestId(0))).toBeDisabled();
});

test('closed with no answer: every guest sees the final bars, and no option is marked You', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture joinedLate tally={pollTally({ counts: [8, 18, 11, 4], answered: 41 })}>
      <PollCard activity={pollActivity('closed')} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(pollOptionPercentTestId(0))).toHaveText('20%');
  for (let i = 0; i < 4; i += 1)
    await expect(page.getByTestId(pollOptionYouTestId(i))).toHaveCount(0);
});

test('answer shown: the correct option is marked Correct in words, not only in colour', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture
      picks={[1]}
      mine={{ picks: [1], revision: 1 }}
      tally={pollTally({ counts: [22, 9, 5, 4], answered: 40 })}
    >
      <PollCard activity={pollActivity('closed', { correct: 1 })} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(pollOptionCorrectTestId(0))).toHaveText('Correct');
  await expect(page.getByTestId(pollOptionTestId(0))).toHaveAccessibleDescription(/Correct/);
  await expect(page.getByTestId(pollOptionCorrectTestId(1))).toHaveCount(0);
  await expect(page.getByTestId(pollOptionYouTestId(1))).toBeVisible();
});

test('at 360 px every option is at least 44 px tall and nothing scrolls sideways', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture
      picks={[0]}
      mine={{ picks: [0], revision: 1 }}
      tally={pollTally({ counts: [1, 0], answered: 1 })}
    >
      <PollCard
        activity={one({
          options: [
            'Supercalifragilisticexpialidociousandthensomemorewordswithoutspaces',
            'A long option that wraps onto a second line on a narrow phone, as sixty can',
          ],
          correct: 1,
          phase: 'closed',
        })}
        presenter="Sam"
      />
    </PollAnswerFixture>,
  );
  for (const radio of await page.getByRole('radio').all()) {
    const row = await radio.locator('xpath=..').boundingBox();
    expect(row!.height).toBeGreaterThanOrEqual(44);
  }
  const widths = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth,
    client: document.documentElement.clientWidth,
  }));
  expect(widths.scroll).toBeLessThanOrEqual(widths.client);
});

test('past five options the list scrolls and the foot stays in view', async ({ mount, page }) => {
  await page.setViewportSize({ width: 360, height: 640 });
  await mount(
    <div style={{ display: 'flex', flexDirection: 'column', height: 460 }}>
      <PollAnswerFixture>
        <PollCard
          activity={one({
            options: ['One', 'Two', 'Three', 'Four', 'Five', 'Six', 'Seven', 'Eight'],
          })}
          presenter="Sam"
        />
      </PollAnswerFixture>
    </div>,
  );
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.tapHint)).toBeInViewport({ ratio: 1 });
  const list = page.getByTestId(POLL_CARD_TEST_IDS.options).locator('xpath=..');
  const sizes = await list.evaluate((el) => ({ scroll: el.scrollHeight, client: el.clientHeight }));
  expect(sizes.scroll).toBeGreaterThan(sizes.client);

  await page.getByTestId(pollOptionTestId(7)).scrollIntoViewIfNeeded();
  await page.getByTestId(pollOptionTestId(7)).click();
  await expect(page.getByTestId(pollOptionTestId(7))).toBeChecked();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.tapHint)).toBeInViewport({ ratio: 1 });
});

test('pick-one sent with live results screenshot at 360', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture picks={[1]} mine={{ picks: [1], revision: 1 }} tally={LIVE_TALLY}>
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.card)).toHaveScreenshot(
    'poll-pick-one-sent-live-360.png',
  );
});

test('pick-one answer shown, not this time, screenshot at 360', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture
      picks={[1]}
      mine={{ picks: [1], revision: 1 }}
      tally={pollTally({ counts: [22, 9, 5, 4], answered: 40 })}
    >
      <PollCard activity={pollActivity('closed', { correct: 1 })} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.card)).toHaveScreenshot(
    'poll-pick-one-miss-360.png',
  );
});
