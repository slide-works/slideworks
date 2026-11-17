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
import { PollAnswerKept } from './PollAnswerKept';

// The pick-several options on a 360 px phone, through the card and the fixture provider (V6, D3). The foot (the count,
// Send, Change my answer) is PollFoot's; these specs only look at it where the options and the foot meet.
const many = (overrides: Partial<PollActivity> = {}) =>
  pollActivity('open', {
    pick: 'upTo',
    max: 2,
    results: 'live',
    roomSeesCounts: true,
    ...overrides,
  });
const QUESTION = 'How big was your last merge request?';
const EIGHT = Array.from({ length: 8 }, (_, i) => `Option number ${i + 1}`);

test.beforeEach(async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 740 });
});

test('disabled reaches the checkboxes: loading, not open yet and closed; never while open or sending', async ({
  mount,
  page,
}) => {
  const boxes = page.getByRole('checkbox');
  const card = await mount(
    <PollAnswerFixture state="loading" pick="upTo" max={2}>
      <PollCard activity={many()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(boxes).toHaveCount(4);
  for (const box of await boxes.all()) await expect(box).toBeDisabled();

  await card.update(
    <PollAnswerFixture pick="upTo" max={2}>
      <PollCard activity={pollActivity('draft', { pick: 'upTo', max: 2 })} presenter="Sam" />
    </PollAnswerFixture>,
  );
  for (const box of await boxes.all()) await expect(box).toBeDisabled();

  await card.update(
    <PollAnswerFixture picks={[1]} mine={{ picks: [1], revision: 1 }} pick="upTo" max={2}>
      <PollCard activity={pollActivity('closed', { pick: 'upTo', max: 2 })} presenter="Sam" />
    </PollAnswerFixture>,
  );
  for (const box of await boxes.all()) await expect(box).toBeDisabled();

  await card.update(
    <PollAnswerFixture pick="upTo" max={2}>
      <PollCard activity={many()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  for (const box of await boxes.all()) await expect(box).toBeEnabled();

  await card.update(
    <PollAnswerFixture state="sending" picks={[2]} pick="upTo" max={2}>
      <PollCard activity={many()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  for (const box of await boxes.all()) await expect(box).toBeEnabled();
});

test('a tap only selects: toggle is called once per tap, nothing is sent, focus stays on the box', async ({
  mount,
  page,
}) => {
  const toggles: number[] = [];
  let sends = 0;
  await mount(
    <PollAnswerFixture
      pick="upTo"
      max={2}
      onToggle={(i) => toggles.push(i)}
      onSend={() => (sends += 1)}
    >
      <PollCard activity={many()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await page.getByTestId(pollOptionTestId(1)).click();
  await expect(page.getByTestId(pollOptionTestId(1))).toBeChecked();
  await expect(page.getByTestId(pollOptionTestId(1))).toBeFocused();
  // A tap anywhere on the row is a tap on its checkbox, and a second tap on the same option unticks it.
  await page.getByText('200 to 1,000 lines').click();
  await expect(page.getByTestId(pollOptionTestId(2))).toBeChecked();
  await page.getByTestId(pollOptionTestId(1)).click();
  await expect(page.getByTestId(pollOptionTestId(1))).not.toBeChecked();
  await page.getByText('Under 50 lines').click();
  await expect(page.getByTestId(pollOptionTestId(0))).toBeChecked();
  await expect.poll(() => toggles).toEqual([1, 2, 1, 0]);
  expect(sends).toBe(0);
});

test('at max the other options are disabled and say why; the hint counts the picks; unticking frees them', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture picks={[0, 2]} pick="upTo" max={2}>
      <PollCard activity={many()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.pickCount)).toContainText('2 of 2 picked');
  await expect(page.getByTestId(pollOptionTestId(0))).toBeEnabled();
  await expect(page.getByTestId(pollOptionTestId(2))).toBeEnabled();
  for (const blocked of [1, 3]) {
    const box = page.getByTestId(pollOptionTestId(blocked));
    await expect(box).toBeDisabled();
    await expect(box).toHaveAccessibleDescription(/most you can/);
  }
  await expect(page.getByTestId(pollOptionTestId(0))).not.toHaveAccessibleDescription(
    /most you can/,
  );

  await page.getByTestId(pollOptionTestId(0)).click();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.pickCount)).toContainText('1 of 2 picked');
  for (const box of await page.getByRole('checkbox').all()) await expect(box).toBeEnabled();
});

test('send: disabled with no picks, enabled with one, and calls send once', async ({
  mount,
  page,
}) => {
  let sends = 0;
  const card = await mount(
    <PollAnswerFixture pick="upTo" max={2} onSend={() => (sends += 1)}>
      <PollCard activity={many()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  const send = page.getByTestId(POLL_CARD_TEST_IDS.send);
  await expect(send).toBeDisabled();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.pickCount)).toContainText('0 of 2 picked');
  await page.getByTestId(pollOptionTestId(3)).click();
  await expect(send).toBeEnabled();
  await send.click();
  await expect.poll(() => sends).toBe(1);
  await card.unmount();
});

test('after sending the boxes lock; Change my answer unlocks them and Send comes back for new picks; a new answer locks them again', async ({
  mount,
  page,
}) => {
  const activity = many();
  const tally = pollTally({ counts: [5, 1, 6, 0], answered: 8 });
  const sent = (revision: number, picks: number[]) => (
    <PollAnswerFixture picks={picks} mine={{ picks, revision }} tally={tally} pick="upTo" max={2}>
      <PollCard activity={activity} presenter="Sam" />
    </PollAnswerFixture>
  );
  const boxes = page.getByRole('checkbox');
  const card = await mount(sent(1, [0, 2]));

  // Locked: Answer sent and Change my answer, every box disabled, the ticks still shown.
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.sent)).toBeVisible();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.send)).toHaveCount(0);
  for (const box of await boxes.all()) await expect(box).toBeDisabled();
  await expect(page.getByTestId(pollOptionTestId(0))).toBeChecked();
  await expect(page.getByTestId(pollOptionYouTestId(2))).toBeVisible();

  // Change my answer: the boxes can be ticked again and focus moves to the first ticked one.
  await page.getByTestId(POLL_CARD_TEST_IDS.change).click();
  await expect(page.getByTestId(pollOptionTestId(0))).toBeEnabled();
  await expect(page.getByTestId(pollOptionTestId(0))).toBeFocused();
  // The fixture's clear empties the picks, so the foot is choosing: the count and a disabled Send.
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.pickCount)).toContainText('0 of 2 picked');
  await page.getByTestId(pollOptionTestId(3)).click();
  await expect(page.getByTestId(pollOptionTestId(3))).toBeChecked();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.send)).toBeEnabled();

  // A new accepted answer (a new revision) locks them again.
  await card.update(sent(2, [3]));
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.change)).toBeVisible();
  for (const box of await boxes.all()) await expect(box).toBeDisabled();
});

test('with the picks kept, Change my answer unlocks them; a new round or a new answer locks them again', async ({
  mount,
  page,
}) => {
  const boxes = page.getByRole('checkbox');
  const card = await mount(<PollAnswerKept activity={many({ round: 'round-a' })} revision={1} />);
  for (const box of await boxes.all()) await expect(box).toBeDisabled();
  await page.getByTestId(POLL_CARD_TEST_IDS.change).click();
  for (const box of await boxes.all()) await expect(box).toBeEnabled();
  await expect(page.getByTestId(pollOptionTestId(1))).toBeFocused();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.pickCount)).toBeVisible();

  await card.update(<PollAnswerKept activity={many({ round: 'round-b' })} revision={1} />);
  for (const box of await boxes.all()) await expect(box).toBeDisabled();

  await page.getByTestId(POLL_CARD_TEST_IDS.change).click();
  for (const box of await boxes.all()) await expect(box).toBeEnabled();
  await card.update(<PollAnswerKept activity={many({ round: 'round-b' })} revision={2} />);
  for (const box of await boxes.all()) await expect(box).toBeDisabled();
});

test('Change my answer shows the count and Send at once with the sent picks ticked, and says so politely', async ({
  mount,
  page,
}) => {
  await mount(<PollAnswerKept activity={many()} revision={1} />);
  await page.getByTestId(POLL_CARD_TEST_IDS.change).click();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.pickCount)).toContainText('1 of 2 picked');
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.send)).toBeEnabled();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.sent)).toHaveCount(0);
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.change)).toHaveCount(0);
  await expect(page.getByTestId(pollOptionTestId(1))).toBeChecked();
  await expect(page.getByTestId(pollOptionTestId(1))).toBeFocused();
  const note = page.locator('[aria-live="polite"]', { hasText: 'You can change your answer now.' });
  await expect(note).toHaveCount(1);
});

test('Send with the picks unchanged sends nothing and locks the boxes again', async ({
  mount,
  page,
}) => {
  let sends = 0;
  await mount(<PollAnswerKept activity={many()} revision={1} onSend={() => (sends += 1)} />);
  await page.getByTestId(POLL_CARD_TEST_IDS.change).click();
  await page.getByTestId(POLL_CARD_TEST_IDS.send).click();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.sent)).toBeVisible();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.change)).toBeVisible();
  for (const box of await page.getByRole('checkbox').all()) await expect(box).toBeDisabled();
  expect(sends).toBe(0);
});

test('a changed tick and Send sends once; the accepted answer locks the boxes', async ({
  mount,
  page,
}) => {
  let sends = 0;
  const view = (revision: number, picks: number[]) => (
    <PollAnswerFixture
      picks={picks}
      mine={{ picks, revision }}
      pick="upTo"
      max={2}
      onSend={() => (sends += 1)}
    >
      <PollCard activity={many()} presenter="Sam" />
    </PollAnswerFixture>
  );
  const card = await mount(view(1, [1]));
  await page.getByTestId(POLL_CARD_TEST_IDS.change).click();
  await page.getByTestId(pollOptionTestId(0)).click();
  await page.getByTestId(POLL_CARD_TEST_IDS.send).click();
  await expect.poll(() => sends).toBe(1);
  await card.update(view(2, [0]));
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.sent)).toBeVisible();
  for (const box of await page.getByRole('checkbox').all()) await expect(box).toBeDisabled();
});

test('pick one is never locked after an answer', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture picks={[1]} mine={{ picks: [1], revision: 1 }}>
      <PollCard activity={pollActivity('open', { pick: 'one', max: null })} presenter="Sam" />
    </PollAnswerFixture>,
  );
  for (const radio of await page.getByRole('radio').all()) await expect(radio).toBeEnabled();
});

test('real checkboxes in a group labelled by the question, each named by its option and the question', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture pick="upTo" max={2}>
      <PollCard activity={many()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  const group = page.getByRole('group', { name: QUESTION });
  await expect(group.getByRole('checkbox')).toHaveCount(4);
  await expect(
    group.getByRole('checkbox', { name: `Under 50 lines ${QUESTION}`, exact: true }),
  ).toHaveAttribute('type', 'checkbox');
});

test('selected is a tick as well as a colour, and every box is at least 44 px tall with no sideways scroll', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture picks={[1]} pick="upTo" max={2}>
      <PollCard activity={many()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  const tick = await page
    .getByTestId(pollOptionTestId(1))
    .evaluate((el) => getComputedStyle(el).backgroundImage);
  expect(tick).toContain('url(');
  const plain = await page
    .getByTestId(pollOptionTestId(0))
    .evaluate((el) => getComputedStyle(el).backgroundImage);
  expect(plain).toBe('none');
  for (let i = 0; i < 4; i += 1) {
    const row = page.getByTestId(pollOptionTestId(i)).locator('xpath=..');
    expect((await row.boundingBox())!.height).toBeGreaterThanOrEqual(44);
  }
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
});

test('answered with bars: shares per option add up past 100%, You and Correct marks sit on their options', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture
      picks={[0, 3]}
      mine={{ picks: [0, 3], revision: 1 }}
      tally={pollTally({ counts: [4, 8, 2, 6], answered: 10 })}
      pick="upTo"
      max={2}
    >
      <PollCard activity={many({ phase: 'closed', correct: 4 })} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(pollOptionPercentTestId(0))).toHaveText('40%');
  await expect(page.getByTestId(pollOptionPercentTestId(1))).toHaveText('80%');
  await expect(page.getByTestId(pollOptionBarTestId(1))).toHaveCount(1);
  await expect(page.getByTestId(pollOptionYouTestId(0))).toHaveText('You');
  await expect(page.getByTestId(pollOptionYouTestId(3))).toHaveText('You');
  await expect(page.getByTestId(pollOptionYouTestId(1))).toHaveCount(0);
  await expect(page.getByTestId(pollOptionCorrectTestId(3))).toHaveText('Correct');
  await expect(page.getByTestId(pollOptionCorrectTestId(0))).toHaveCount(0);
});

test('no bars or percentages before the card passes counts', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture picks={[1]} pick="upTo" max={2}>
      <PollCard activity={many()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(pollOptionBarTestId(0))).toHaveCount(0);
  await expect(page.getByTestId(pollOptionPercentTestId(0))).toHaveCount(0);
});

test('with eight options the list scrolls at 360 x 640 and the foot stays in view', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 640 });
  await mount(
    <div style={{ display: 'flex', height: 560, flexDirection: 'column' }}>
      <PollAnswerFixture picks={[0]} pick="upTo" max={3}>
        <PollCard activity={many({ options: EIGHT, max: 3 })} presenter="Sam" />
      </PollAnswerFixture>
    </div>,
  );
  const list = page.getByTestId(POLL_CARD_TEST_IDS.optionsMany).locator('xpath=..');
  const scrolls = await list.evaluate((el) => el.scrollHeight > el.clientHeight);
  expect(scrolls).toBe(true);
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.send)).toBeInViewport();
  await page.getByTestId(pollOptionTestId(7)).scrollIntoViewIfNeeded();
  await expect(page.getByTestId(pollOptionTestId(7))).toBeInViewport();
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.send)).toBeInViewport();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
});

test('pick several, two ticked, at the limit screenshot at 360', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture picks={[0, 2]} pick="upTo" max={2}>
      <PollCard activity={many()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page).toHaveScreenshot('pick-several-at-limit.png', { maxDiffPixels: 100 });
});

// Forced colours (a high-contrast theme) replace backgrounds and borders and keep images, so a drawn tick or dot can
// vanish. The native control draws its state in system colours, and a ticked box looks different from an empty one.
test('forced colours: a checked checkbox differs from an unchecked one in its native state, not only in a drawn tick', async ({
  mount,
  page,
}) => {
  await page.emulateMedia({ forcedColors: 'active' });
  await mount(
    <PollAnswerFixture picks={[1]} pick="upTo" max={3}>
      <PollCard activity={many({ max: 3 })} presenter="Sam" />
    </PollAnswerFixture>,
  );
  const checked = page.getByTestId(pollOptionTestId(1));
  const empty = page.getByTestId(pollOptionTestId(0));
  expect(await checked.evaluate((el) => getComputedStyle(el).appearance)).not.toBe('none');
  const a = await checked.screenshot();
  const b = await empty.screenshot();
  expect(a.equals(b)).toBe(false);
});

test('forced colours: a selected radio differs from an unselected one', async ({ mount, page }) => {
  await page.emulateMedia({ forcedColors: 'active' });
  await mount(
    <PollAnswerFixture picks={[1]}>
      <PollCard activity={pollActivity('open', { pick: 'one', max: null })} presenter="Sam" />
    </PollAnswerFixture>,
  );
  const selected = page.getByTestId(pollOptionTestId(1));
  const other = page.getByTestId(pollOptionTestId(0));
  expect(await selected.evaluate((el) => getComputedStyle(el).appearance)).not.toBe('none');
  const a = await selected.screenshot();
  const b = await other.screenshot();
  expect(a.equals(b)).toBe(false);
});
