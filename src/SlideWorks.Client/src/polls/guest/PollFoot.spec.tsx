import { test, expect } from '@playwright/experimental-ct-react';
import { guestPoll, pollActivity, pollTally } from '../../test-support/fixtures/pollStates';
import { POLL_CARD_TEST_IDS, pollOptionTestId } from '../testIds';
import type { PollActivity } from '../types';
import { PollAnswerFixture } from './PollAnswerFixture';
import { PollCard } from './PollCard';

// What the card says under the options, in every pick-one state, on a 360 px phone, through the card and the fixture.
const one = (overrides: Partial<PollActivity> = {}) =>
  pollActivity('open', {
    pick: 'one',
    max: null,
    results: 'live',
    roomSeesCounts: true,
    ...overrides,
  });
const QUESTION = 'How big was your last merge request?';
const T = POLL_CARD_TEST_IDS;
const FOOT_LINES = [
  T.tapHint,
  T.pickCount,
  T.send,
  T.sending,
  T.sent,
  T.answered,
  T.resultsLater,
  T.changeHint,
  T.change,
  T.closed,
  T.noAnswer,
  T.joinedLate,
  T.notOpen,
  T.failed,
];

test.beforeEach(async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 740 });
});

test('loading: the foot says nothing yet', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture state="loading">
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.foot)).toBeAttached();
  for (const id of FOOT_LINES) await expect(page.getByTestId(id), id).toHaveCount(0);
});

test('not open yet: the presenter hasn’t opened it, and the options show greyed out', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture>
      <PollCard activity={guestPoll('notOpen').activity!} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.notOpen)).toHaveText(
    'Sam hasn’t opened this poll yet. It opens by itself, so keep this page open.',
  );
  await expect(page.getByTestId(pollOptionTestId(0))).toBeDisabled();
  await expect(page.getByTestId(T.tapHint)).toHaveCount(0);
});

test('open and unanswered: tap an answer to send it', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture tally={pollTally({ counts: [7, 17, 10, 4], answered: 38 })}>
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.tapHint)).toContainText('Tap an answer to send it.');
  await expect(page.getByTestId(T.send)).toHaveCount(0);
  await expect(page.getByTestId(T.answered)).toHaveCount(0);
});

test('sending: the foot says the answer is on its way', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture state="sending" picks={[1]}>
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.sending)).toHaveText('Sending your answer…');
  await expect(page.getByTestId(T.sent)).toHaveCount(0);
  await expect(page.getByTestId(T.tapHint)).toHaveCount(0);
});

test('sent with live results: answer sent, the count so far, and how to change it', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture
      picks={[1]}
      mine={{ picks: [1], revision: 1 }}
      tally={pollTally({ counts: [7, 17, 10, 4], answered: 38 })}
    >
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.sent)).toHaveText('Answer sent');
  await expect(page.getByTestId(T.answered)).toHaveText('38 answers so far');
  await expect(page.getByTestId(T.changeHint)).toHaveText('Tap another option to change it');
  await expect(page.getByTestId(T.resultsLater)).toHaveCount(0);
});

test('sent with results hidden: answer sent, and results show when the presenter closes the poll', async ({
  mount,
  page,
}) => {
  const hidden = guestPoll('hidden');
  await mount(
    <PollAnswerFixture picks={hidden.mine!.picks} mine={hidden.mine} tally={hidden.tally}>
      <PollCard activity={hidden.activity!} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.sent)).toBeVisible();
  await expect(page.getByTestId(T.resultsLater)).toHaveText(
    'Results show here and on the big screen when Sam closes the poll.',
  );
});

test('failed: the message shows, and Send again names the question and sends', async ({
  mount,
  page,
}) => {
  let sends = 0;
  await mount(
    <PollAnswerFixture
      state="failed"
      picks={[1]}
      error="too_many_answers"
      onSend={() => (sends += 1)}
    >
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByRole('alert')).toContainText('Your answer wasn’t sent.');
  await expect(page.getByTestId(T.failed)).toHaveText('too_many_answers');
  await page.getByRole('button', { name: `Send again: ${QUESTION}` }).click();
  await expect.poll(() => sends).toBe(1);
});

test('closed with an answer: poll closed with the final count, and no no-answer line', async ({
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
  await expect(page.getByTestId(T.closed)).toHaveText(/Poll closed\s*41 answers/);
  await expect(page.getByTestId(T.noAnswer)).toHaveCount(0);
  await expect(page.getByTestId(T.joinedLate)).toHaveCount(0);
  await expect(page.getByTestId(T.sent)).toHaveCount(0);
});

test('closed with no answer: you didn’t send an answer for this one', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture tally={pollTally({ counts: [8, 18, 11, 4], answered: 41 })}>
      <PollCard activity={pollActivity('closed')} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.noAnswer)).toHaveText('You didn’t send an answer for this one.');
  await expect(page.getByTestId(T.joinedLate)).toHaveCount(0);
});

test('closed, joined late: you joined after this poll closed', async ({ mount, page }) => {
  const late = guestPoll('joinedLate');
  await mount(
    <PollAnswerFixture joinedLate={late.joinedLate} tally={late.tally}>
      <PollCard activity={late.activity!} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.joinedLate)).toHaveText('You joined after this poll closed.');
  await expect(page.getByTestId(T.noAnswer)).toHaveCount(0);
});

test('pick several: the pick count, Send only with a pick, and Change my answer after sending', async ({
  mount,
  page,
}) => {
  const many = pollActivity('open');
  let sends = 0;
  let clears = 0;
  const card = await mount(
    <PollAnswerFixture pick="upTo" max={2} picks={[]} onSend={() => (sends += 1)}>
      <PollCard activity={many} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.pickCount)).toHaveText(/0 of 2 picked/);
  await expect(page.getByRole('button', { name: `Send answer: ${QUESTION}` })).toBeDisabled();

  await card.update(
    <PollAnswerFixture pick="upTo" max={2} picks={[0]} onSend={() => (sends += 1)}>
      <PollCard activity={many} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.pickCount)).toHaveText(/1 of 2 picked/);
  await page.getByRole('button', { name: `Send answer: ${QUESTION}` }).click();
  await expect.poll(() => sends).toBe(1);

  await card.update(
    <PollAnswerFixture
      pick="upTo"
      max={2}
      picks={[0, 3]}
      mine={{ picks: [0, 3], revision: 1 }}
      tally={pollTally({ counts: null, answered: 12 })}
      onClear={() => (clears += 1)}
    >
      <PollCard activity={many} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.sent)).toBeVisible();
  await expect(page.getByTestId(T.send)).toHaveCount(0);
  await page.getByRole('button', { name: `Change my answer: ${QUESTION}` }).click();
  await expect.poll(() => clears).toBe(1);
  await expect(page.getByTestId(T.pickCount)).toHaveText(/0 of 2 picked/);
  await expect(page.getByTestId(T.send)).toBeVisible();
});

test('the count so far is in a polite live region, announced at most once every 5 seconds', async ({
  mount,
  page,
}) => {
  await page.clock.install();
  const at = (answered: number) => (
    <PollAnswerFixture
      picks={[1]}
      mine={{ picks: [1], revision: 1 }}
      tally={pollTally({ counts: [1, answered - 1, 0, 0], answered })}
    >
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>
  );
  const region = page.getByTestId(T.foot).locator('[aria-live="polite"]');
  const card = await mount(at(38));
  await expect(region).toHaveText('38 answers so far');

  await card.update(at(39));
  await page.clock.runFor(100);
  await expect(page.getByTestId(T.answered)).toHaveText('39 answers so far');
  await expect(region).toHaveText('39 answers so far');

  await card.update(at(40));
  await card.update(at(41));
  await page.clock.runFor(1000);
  await expect(page.getByTestId(T.answered)).toHaveText('41 answers so far');
  await expect(region).toHaveText('39 answers so far');

  await page.clock.runFor(4500);
  await expect(region).toHaveText('41 answers so far');
});

test('the foot keeps focus off itself: tapping an option leaves focus on it while the foot changes', async ({
  mount,
  page,
}) => {
  const card = await mount(
    <PollAnswerFixture>
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await page.getByTestId(pollOptionTestId(2)).click();
  await card.update(
    <PollAnswerFixture
      picks={[2]}
      mine={{ picks: [2], revision: 1 }}
      tally={pollTally({ counts: [0, 0, 1, 0], answered: 1 })}
    >
      <PollCard activity={one()} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.sent)).toBeVisible();
  await expect(page.getByTestId(pollOptionTestId(2))).toBeFocused();
});

test('not open yet screenshot at 360', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture>
      <PollCard activity={guestPoll('notOpen').activity!} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.card)).toHaveScreenshot('poll-pick-one-not-open-360.png');
});

test('sent with results hidden screenshot at 360', async ({ mount, page }) => {
  const hidden = guestPoll('hidden');
  await mount(
    <PollAnswerFixture picks={hidden.mine!.picks} mine={hidden.mine} tally={hidden.tally}>
      <PollCard activity={hidden.activity!} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(T.card)).toHaveScreenshot('poll-pick-one-sent-hidden-360.png');
});
