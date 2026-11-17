import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { guestPoll, pollActivity, pollTally } from '../../test-support/fixtures/pollStates';
import { POLL_CARD_TEST_IDS } from '../testIds';
import { PollAnswerFixture } from './PollAnswerFixture';
import { PollCard } from './PollCard';

const PARTS = {
  options: POLL_CARD_TEST_IDS.options,
  optionsMany: POLL_CARD_TEST_IDS.optionsMany,
  foot: POLL_CARD_TEST_IDS.foot,
  verdict: POLL_CARD_TEST_IDS.verdict,
  conflict: POLL_CARD_TEST_IDS.conflict,
  queued: POLL_CARD_TEST_IDS.queued,
} as const;

type Part = keyof typeof PARTS;

// Exactly the named parts are mounted on the card, and none of the others.
async function expectParts(page: Page, mounted: Part[]) {
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.card)).toBeVisible();
  for (const [part, id] of Object.entries(PARTS))
    await expect(page.getByTestId(id), part).toHaveCount(mounted.includes(part as Part) ? 1 : 0);
}

test('draft: the options and the foot, nothing else', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture>
      <PollCard activity={pollActivity('draft')} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expectParts(page, ['options', 'foot']);
});

test('loading: the options and the foot while the first read is in flight', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture state="loading">
      <PollCard activity={pollActivity('open', { pick: 'one', max: null })} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expectParts(page, ['options', 'foot']);
});

test('joined late: the closed card shows the options and the foot, and no verdict', async ({
  mount,
  page,
}) => {
  const late = guestPoll('joinedLate');
  await mount(
    <PollAnswerFixture joinedLate tally={late.tally}>
      <PollCard activity={late.activity!} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expectParts(page, ['options', 'foot']);
});

test('choosing one: the radio group labelled by the question, and the foot', async ({
  mount,
  page,
}) => {
  const activity = pollActivity('open', { pick: 'one', max: null });
  await mount(
    <PollAnswerFixture>
      <PollCard activity={activity} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expectParts(page, ['options', 'foot']);
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.kind)).toHaveText('Poll · Pick one');
  await expect(page.getByRole('radiogroup', { name: activity.question })).toHaveAttribute(
    'data-testid',
    POLL_CARD_TEST_IDS.options,
  );
});

test('choosing several: the checkbox group labelled by the question, and the foot', async ({
  mount,
  page,
}) => {
  const activity = pollActivity('open');
  await mount(
    <PollAnswerFixture pick="upTo" max={2} picks={[0]}>
      <PollCard activity={activity} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expectParts(page, ['optionsMany', 'foot']);
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.kind)).toHaveText('Poll · Pick up to 2');
  await expect(page.getByRole('group', { name: activity.question })).toHaveAttribute(
    'data-testid',
    POLL_CARD_TEST_IDS.optionsMany,
  );
});

test('sent: the options and the foot, with no conflict or queued note', async ({ mount, page }) => {
  const sent = guestPoll('hidden');
  await mount(
    <PollAnswerFixture picks={sent.mine!.picks} mine={sent.mine} tally={sent.tally}>
      <PollCard activity={sent.activity!} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expectParts(page, ['options', 'foot']);
});

test('sending: the foot stays while the answer is in flight', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture state="sending" picks={[1]}>
      <PollCard activity={pollActivity('open', { pick: 'one', max: null })} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expectParts(page, ['options', 'foot']);
});

test('closed: the options and the foot, and no verdict before the answer is shown', async ({
  mount,
  page,
}) => {
  await mount(
    <PollAnswerFixture
      picks={[1]}
      mine={{ picks: [1], revision: 1 }}
      tally={pollTally({ counts: [8, 18, 10, 4], answered: 40 })}
    >
      <PollCard activity={pollActivity('closed')} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expectParts(page, ['options', 'foot']);
});

test('answer shown: the foot and the verdict', async ({ mount, page }) => {
  const shown = guestPoll('answered');
  await mount(
    <PollAnswerFixture
      pick="upTo"
      max={2}
      picks={shown.mine!.picks}
      mine={shown.mine}
      tally={shown.tally}
    >
      <PollCard activity={shown.activity!} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expectParts(page, ['optionsMany', 'foot', 'verdict']);
});

test('conflict: the conflict takes the foot’s place, the pick kept', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture state="conflict" picks={[2]}>
      <PollCard
        activity={pollActivity('open', { pick: 'one', max: null, number: 2 })}
        presenter="Sam"
      />
    </PollAnswerFixture>,
  );
  await expectParts(page, ['options', 'conflict']);
});

test('queued: the queued note takes the foot’s place', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture state="queued" picks={[1]}>
      <PollCard activity={pollActivity('open', { pick: 'one', max: null })} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expectParts(page, ['options', 'queued']);
});

test('failed: the foot shows the refusal, so nothing fails silently', async ({ mount, page }) => {
  await mount(
    <PollAnswerFixture state="failed" picks={[1]} error="too_many_answers">
      <PollCard activity={pollActivity('open', { pick: 'one', max: null })} presenter="Sam" />
    </PollAnswerFixture>,
  );
  await expectParts(page, ['options', 'foot']);
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.failed)).toHaveText('too_many_answers');
});

test('the round shows from round 2 on, and the privacy line names the presenter', async ({
  mount,
  page,
}) => {
  const card = await mount(
    <PollAnswerFixture>
      <PollCard activity={pollActivity('open')} presenter="Sam Rivera" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.round)).toHaveCount(0);
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.question)).toHaveText(
    'How big was your last merge request?',
  );
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.privacy)).toHaveText(
    'Private: Sam Rivera sees totals only',
  );

  await card.update(
    <PollAnswerFixture>
      <PollCard activity={pollActivity('open', { number: 2 })} presenter="Sam Rivera" />
    </PollAnswerFixture>,
  );
  await expect(page.getByTestId(POLL_CARD_TEST_IDS.round)).toHaveText('Round 2');
});
