import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { guestQuestion, guestQuestions, qaRoom } from '../../test-support/fixtures/qaStates';
import {
  QA_LIST_TEST_IDS,
  questionAnsweredTestId,
  questionHiddenTestId,
  questionMetaTestId,
  questionNoVoteTestId,
  questionOnScreenTestId,
  questionRowTestId,
  questionTextTestId,
  questionUpvoteTestId,
  questionVotesTestId,
  questionWaitingTestId,
  questionYouTestId,
} from '../testIds';
import type { GuestQuestion } from '../types';
import { AskBar } from './AskBar';
import { AskSheetFixture } from './AskSheetFixture';
import { QuestionList } from './QuestionList';
import { QuestionsFixture, type QuestionsFixtureProps } from './QuestionsFixture';
import { QA_NETWORK_ERROR } from './QuestionsLive';

// The Questions tab's list (Q8; mockup phone states 2, 3, 6, 7, 8, 10, 12) over QuestionsFixture on a 360 px phone. The
// view sample holds 1 (14 votes, upvoted), 2 (9, on screen), 3 (answered, 6), 4 (the guest's own, waiting) and 6 (3).
test.beforeEach(async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 740 });
});

const VIEW = guestQuestions('view');

type ListProps = Omit<QuestionsFixtureProps, 'children'> & { onShow?: () => void };

// The list and the Ask bar as GuestTabs lays them out in the Questions panel, on the guest page's dark background.
function list({ onShow, ...props }: ListProps = {}) {
  return (
    <QuestionsFixture room={VIEW.room} questions={VIEW.questions} mine={VIEW.mine} {...props}>
      <AskSheetFixture onShow={onShow}>
        <div
          style={{
            height: 700,
            display: 'flex',
            flexDirection: 'column',
            gap: 12,
            padding: '16px 16px 12px',
            background: 'var(--sw-night)',
          }}
        >
          <QuestionList presenter="Sam" />
          <AskBar />
        </div>
      </AskSheetFixture>
    </QuestionsFixture>
  );
}

const rowIds = (page: Page, testId: string) =>
  page
    .getByTestId(testId)
    .locator(':scope > li')
    .evaluateAll((rows) => rows.map((r) => r.getAttribute('data-testid')));

const upvote = (page: Page, number: number) => page.getByTestId(questionUpvoteTestId(number));

// The list's polite live region, which says how many questions are open.
const announced = (page: Page) =>
  page.getByTestId(QA_LIST_TEST_IDS.list).locator('[aria-live="polite"]');

test('Top sorts by votes with ties to the older question, the guest’s held ones pinned on top in number order', async ({
  mount,
  page,
}) => {
  const tied = [
    ...VIEW.questions,
    guestQuestion(6, { number: 7, votes: 9, text: 'A later tie' }),
    guestQuestion(4, { number: 9, state: 'hidden', text: 'A later held question' }),
  ];
  await mount(list({ questions: tied }));

  await expect(page.getByTestId(QA_LIST_TEST_IDS.sortTop)).toHaveAttribute('aria-pressed', 'true');
  expect(await rowIds(page, QA_LIST_TEST_IDS.rows)).toEqual([
    questionRowTestId(4),
    questionRowTestId(9),
    questionRowTestId(1),
    questionRowTestId(2),
    questionRowTestId(7),
    questionRowTestId(6),
  ]);
  await expect(page.getByTestId(QA_LIST_TEST_IDS.heading)).toHaveText('Questions');
  await expect(page.getByTestId(QA_LIST_TEST_IDS.openCount)).toHaveText('4 open');
});

test('New puts the newest first and tells the hook', async ({ mount, page }) => {
  const sorts: string[] = [];
  await mount(list({ onSort: (sort) => sorts.push(sort) }));

  await page.getByTestId(QA_LIST_TEST_IDS.sortNew).click();

  await expect(page.getByTestId(QA_LIST_TEST_IDS.sortNew)).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByTestId(QA_LIST_TEST_IDS.sortTop)).toHaveAttribute('aria-pressed', 'false');
  expect(await rowIds(page, QA_LIST_TEST_IDS.rows)).toEqual([
    questionRowTestId(4),
    questionRowTestId(6),
    questionRowTestId(2),
    questionRowTestId(1),
  ]);
  await expect.poll(() => sorts).toEqual(['new']);
});

test('an upvote toggles aria-pressed and the count in its name', async ({ mount, page }) => {
  const toggled: number[] = [];
  await mount(list({ onToggleVote: (n) => toggled.push(n) }));
  const six = upvote(page, 6);

  await expect(upvote(page, 1)).toHaveAttribute('aria-pressed', 'true');
  await expect(six).toHaveAttribute('aria-pressed', 'false');
  await expect(six).toHaveAccessibleName('Upvote, 3 votes: How do you handle database migrations?');

  await six.click();

  await expect(six).toHaveAttribute('aria-pressed', 'true');
  await expect(six).toHaveAccessibleName('Upvote, 4 votes: How do you handle database migrations?');
  await expect(page.getByTestId(questionVotesTestId(6))).toHaveText('4');

  await six.click();

  await expect(six).toHaveAttribute('aria-pressed', 'false');
  await expect(six).toHaveAccessibleName('Upvote, 3 votes: How do you handle database migrations?');
  await expect.poll(() => toggled).toEqual([6, 6]);
});

test('a waiting question shows only its tag, with no upvote and no count', async ({
  mount,
  page,
}) => {
  await mount(list());

  await expect(page.getByTestId(questionNoVoteTestId(4))).toHaveText('…');
  await expect(upvote(page, 4)).toHaveCount(0);
  await expect(page.getByTestId(questionVotesTestId(4))).toHaveCount(0);
  await expect(page.getByTestId(questionTextTestId(4))).toHaveText('Who reviews the slices?');
  await expect(page.getByTestId(questionYouTestId(4))).toHaveText('You');
  await expect(page.getByTestId(questionMetaTestId(4))).toHaveText('Anonymous · 2 min');
  await expect(page.getByTestId(questionWaitingTestId(4))).toHaveText('Waiting for Sam');
  await expect(page.getByTestId(questionHiddenTestId(4))).toHaveCount(0);
  await expect(page.getByTestId(questionOnScreenTestId(4))).toHaveCount(0);
});

test('the guest’s hidden question is pinned and says it is not shown to the room', async ({
  mount,
  page,
}) => {
  const hidden = guestQuestions('ownHidden');
  await mount(list({ questions: hidden.questions, mine: hidden.mine }));

  expect((await rowIds(page, QA_LIST_TEST_IDS.rows))[0]).toBe(questionRowTestId(4));
  await expect(upvote(page, 4)).toHaveCount(0);
  await expect(page.getByTestId(questionNoVoteTestId(4))).toHaveText('–');
  await expect(page.getByTestId(questionHiddenTestId(4))).toHaveText('Not shown to the room');
  await expect(page.getByTestId(questionWaitingTestId(4))).toHaveCount(0);
});

test('the question on the big screen is tagged', async ({ mount, page }) => {
  await mount(list());

  await expect(page.getByTestId(questionOnScreenTestId(2))).toHaveText('On screen now');
  await expect(page.getByTestId(questionOnScreenTestId(1))).toHaveCount(0);
});

test('answered questions sit in their own section by votes, with a tick and no upvote', async ({
  mount,
  page,
}) => {
  const answered: GuestQuestion[] = [
    ...VIEW.questions,
    guestQuestion(1, {
      number: 8,
      state: 'answered',
      votes: 7,
      text: 'A later answer with more votes',
    }),
  ];
  await mount(list({ questions: answered }));
  const section = page.getByTestId(QA_LIST_TEST_IDS.answered);

  await expect(page.getByTestId(QA_LIST_TEST_IDS.answeredHeading)).toHaveText('Answered · 2');
  expect(
    await section
      .getByRole('listitem')
      .evaluateAll((rows) => rows.map((r) => r.getAttribute('data-testid'))),
  ).toEqual([questionRowTestId(8), questionRowTestId(3)]);
  await expect(section.getByRole('button')).toHaveCount(0);
  await expect(upvote(page, 3)).toHaveCount(0);
  await expect(page.getByTestId(questionAnsweredTestId(3))).toHaveText('✓');
  await expect(page.getByTestId(questionMetaTestId(3))).toHaveText('Sam K · 8 min');
  await expect(page.getByTestId(questionVotesTestId(3))).toHaveText(' · 6 votes');
  await expect(
    page.getByTestId(QA_LIST_TEST_IDS.rows).getByTestId(questionRowTestId(3)),
  ).toHaveCount(0);
});

test('with no questions the empty state and the Ask button are all there is', async ({
  mount,
  page,
}) => {
  let shown = 0;
  await mount(list({ questions: [], mine: { picks: [], revision: 0 }, onShow: () => shown++ }));

  await expect(page.getByTestId(QA_LIST_TEST_IDS.emptyTitle)).toHaveText('No questions yet');
  await expect(page.getByTestId(QA_LIST_TEST_IDS.emptyText)).toHaveText(
    'Ask the first one. Other people can upvote it, and Sam sees which questions the room wants answered.',
  );
  await expect(page.getByTestId(QA_LIST_TEST_IDS.heading)).toHaveCount(0);

  await page.getByTestId(QA_LIST_TEST_IDS.ask).click();

  await expect(page.getByTestId(QA_LIST_TEST_IDS.ask)).toHaveText('Ask a question');
  await expect.poll(() => shown).toBe(1);
});

test('empty and closed shows no invitation to ask, only the title and the closed line', async ({
  mount,
  page,
}) => {
  await mount(
    list({
      room: qaRoom('open', { questions: 'closed' }),
      questions: [],
      mine: { picks: [], revision: 0 },
    }),
  );

  await expect(page.getByTestId(QA_LIST_TEST_IDS.emptyTitle)).toHaveText('No questions yet');
  await expect(page.getByTestId(QA_LIST_TEST_IDS.emptyText)).toHaveCount(0);
  await expect(page.getByTestId(QA_LIST_TEST_IDS.closed)).toBeVisible();
  await expect(page.getByTestId(QA_LIST_TEST_IDS.askBar)).toHaveCount(0);
});

test('closed keeps the list and upvotes and replaces asking with a line', async ({
  mount,
  page,
}) => {
  await mount(list({ room: qaRoom('open', { questions: 'closed' }) }));

  await expect(page.getByTestId(QA_LIST_TEST_IDS.closed)).toHaveText(
    'Sam has closed questions. You can still upvote.',
  );
  await expect(page.getByTestId(QA_LIST_TEST_IDS.askBar)).toHaveCount(0);
  await expect(upvote(page, 6)).toBeEnabled();
});

test('off here keeps the list and upvotes and replaces asking with a line', async ({
  mount,
  page,
}) => {
  await mount(list({ room: qaRoom('open', { questions: 'offHere' }) }));

  await expect(page.getByTestId(QA_LIST_TEST_IDS.offHere)).toHaveText(
    'Questions are off on this slide. You can still upvote.',
  );
  await expect(page.getByTestId(QA_LIST_TEST_IDS.closed)).toHaveCount(0);
  await expect(page.getByTestId(QA_LIST_TEST_IDS.askBar)).toHaveCount(0);
  await expect(upvote(page, 6)).toBeEnabled();
});

test('loading shows nothing to tap until the first read lands, not even Ask', async ({
  mount,
  page,
}) => {
  await mount(list({ state: 'loading', room: null, questions: [] }));

  await expect(page.getByTestId(QA_LIST_TEST_IDS.loading)).toBeVisible();
  await expect(page.getByTestId(QA_LIST_TEST_IDS.list)).toHaveAttribute('aria-busy', 'true');
  await expect(page.getByTestId(QA_LIST_TEST_IDS.list).getByRole('button')).toHaveCount(0);
  await expect(page.getByTestId(QA_LIST_TEST_IDS.empty)).toHaveCount(0);
  await expect(page.getByTestId(QA_LIST_TEST_IDS.askBar)).toHaveCount(0);
});

test('failed says why and Try again reads the list again', async ({ mount, page }) => {
  let retries = 0;
  await mount(
    list({
      state: 'failed',
      room: null,
      questions: [],
      error: QA_NETWORK_ERROR,
      errorFrom: 'read',
      onRetry: () => retries++,
    }),
  );

  await expect(page.getByTestId(QA_LIST_TEST_IDS.failed)).toContainText(
    "Couldn't load the questions.",
  );
  await expect(page.getByTestId(QA_LIST_TEST_IDS.failed)).toContainText(QA_NETWORK_ERROR);

  await page.getByTestId(QA_LIST_TEST_IDS.failedRetry).click();

  await expect(page.getByTestId(QA_LIST_TEST_IDS.failedRetry)).toHaveText('Try again');
  await expect.poll(() => retries).toBe(1);
  await expect(page.getByTestId(QA_LIST_TEST_IDS.askBar)).toHaveCount(0);
});

test('a failed upvote shows the hook’s error under the list, as a sentence', async ({
  mount,
  page,
}) => {
  await mount(list({ error: 'too_many_votes', errorFrom: 'vote' }));

  await expect(page.getByTestId(QA_LIST_TEST_IDS.error)).toHaveText(
    'You’ve changed your upvotes a lot in the last minute. Wait a moment, then try again.',
  );
  await expect(page.getByTestId(QA_LIST_TEST_IDS.error)).toHaveAttribute('role', 'alert');
});

test('an ask’s failure is left to the ask sheet, not shown under the list', async ({
  mount,
  page,
}) => {
  await mount(list({ asking: 'failed', error: 'questions_closed', errorFrom: 'ask' }));

  await expect(page.getByTestId(QA_LIST_TEST_IDS.list)).toBeVisible();
  await expect(page.getByTestId(QA_LIST_TEST_IDS.error)).toHaveCount(0);
});

test('after a failed ask, a failed upvote still shows its message under the list', async ({
  mount,
  page,
}) => {
  const component = await mount(
    list({ asking: 'failed', error: 'questions_closed', errorFrom: 'ask' }),
  );
  await expect(page.getByTestId(QA_LIST_TEST_IDS.error)).toHaveCount(0);

  await component.update(list({ asking: 'failed', error: 'too_many_votes', errorFrom: 'vote' }));

  await expect(page.getByTestId(QA_LIST_TEST_IDS.error)).toHaveText(
    'You’ve changed your upvotes a lot in the last minute. Wait a moment, then try again.',
  );
});

test('the live region says how many questions are open, and a tally that only moves votes says nothing new', async ({
  mount,
  page,
}) => {
  const component = await mount(list());
  await expect(announced(page)).toHaveText('3 open questions');

  const tallied = VIEW.questions.map((q) => ({ ...q, votes: q.votes + 5 }));
  await component.update(list({ questions: tallied }));
  await expect(page.getByTestId(questionVotesTestId(6))).toHaveText('8');
  await expect(announced(page)).toHaveText('3 open questions');

  const asked = [...tallied, guestQuestion(6, { number: 7, text: 'A new question' })];
  await component.update(list({ questions: asked }));
  await expect(announced(page)).toHaveText('4 open questions');
});

test('a row can take focus from code, so focus can move to a new question', async ({
  mount,
  page,
}) => {
  await mount(list());
  const row = page.getByTestId(questionRowTestId(6));

  await row.focus();

  await expect(row).toBeFocused();
  await expect(row).toHaveAttribute('tabindex', '-1');
});

test('a long question and name wrap and nothing scrolls sideways', async ({ mount, page }) => {
  const long = guestQuestion(1, {
    text: 'W'.repeat(120) + ' ' + 'how do you split a big change into slices '.repeat(2),
    name: 'W'.repeat(30),
  });
  await mount(list({ questions: [long, ...VIEW.questions.slice(1)] }));
  const row = page.getByTestId(questionRowTestId(1));

  await expect(row).toBeVisible();
  const box = await row.boundingBox();
  expect(box!.x + box!.width).toBeLessThanOrEqual(360);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(360);
  expect(box!.height).toBeGreaterThan(60);
  // Every line of the row stays inside its own box: the text and the name wrap rather than overflow.
  const overflowing = await row
    .locator('*')
    .evaluateAll((els) => els.filter((el) => el.scrollWidth > el.clientWidth).length);
  expect(overflowing).toBe(0);
});

test('screenshot the list sorted by Top', async ({ mount, page }) => {
  await mount(list());
  await expect(page).toHaveScreenshot('qa-list-top.png');
});

test('screenshot answered, on screen and closed', async ({ mount, page }) => {
  await mount(list({ room: qaRoom('open', { questions: 'closed' }) }));
  await expect(page).toHaveScreenshot('qa-list-closed.png');
});

test('screenshot no questions yet', async ({ mount, page }) => {
  await mount(list({ questions: [], mine: { picks: [], revision: 0 } }));
  await expect(page).toHaveScreenshot('qa-list-empty.png');
});

test('screenshot failed', async ({ mount, page }) => {
  await mount(list({ state: 'failed', questions: [], error: QA_NETWORK_ERROR }));
  await expect(page).toHaveScreenshot('qa-list-failed.png');
});
