import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { audienceState } from '../../test-support/fixtures/guestStates';
import { guestQuestions, qaRoom } from '../../test-support/fixtures/qaStates';
import { QuestionsProbe } from '../../test-support/QuestionsProbe';
import { stubQuestionsApi } from '../../test-support/stubQuestionsApi';
import { QA_ASK_SHEET_TEST_IDS, QA_TABS_TEST_IDS } from '../testIds';
import type { QaRoom } from '../types';
import { AskSheet } from './AskSheet';
import { AskSheetFixture } from './AskSheetFixture';
import { GuestQa } from './GuestQa';
import { PhoneReactionsFixture } from './PhoneReactionsFixture';
import { QuestionsFixture } from './QuestionsFixture';
import type { AskingState } from './useQuestions';

// The phone's seams (Q8) on a 360 px phone: the tab shell, the fixture providers and the questions stub API. Specs here
// assert test ids, hook values and recorded calls, never a part's text, so the parts and live providers that replace the
// stubs keep them green.
test.beforeEach(async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 740 });
});

const live = (room: QaRoom | null) => audienceState({ room });

const slideChild = <div data-testid="slide-content">slide</div>;

test('the two tabs exist and switch the panel', async ({ mount, page }) => {
  await stubQuestionsApi(page);
  await mount(
    <GuestQa code="482913" state={live(qaRoom('open'))} status="live" presenter="Sam Rivera">
      {slideChild}
    </GuestQa>,
  );
  const slideTab = page.getByTestId(QA_TABS_TEST_IDS.slideTab);
  const questionsTab = page.getByTestId(QA_TABS_TEST_IDS.questionsTab);

  await expect(page.getByTestId(QA_TABS_TEST_IDS.bar)).toHaveAttribute('role', 'tablist');
  await expect(slideTab).toHaveAttribute('aria-selected', 'true');
  await expect(questionsTab).toHaveAttribute('aria-selected', 'false');
  await expect(page.getByTestId(QA_TABS_TEST_IDS.slidePanel)).toBeVisible();
  await expect(page.getByTestId('slide-content')).toBeVisible();
  await expect(page.getByTestId(QA_TABS_TEST_IDS.questionsPanel)).toBeHidden();

  await questionsTab.click();

  await expect(questionsTab).toHaveAttribute('aria-selected', 'true');
  await expect(slideTab).toHaveAttribute('aria-selected', 'false');
  await expect(page.getByTestId(QA_TABS_TEST_IDS.questionsPanel)).toBeVisible();
  await expect(page.getByTestId(QA_TABS_TEST_IDS.slidePanel)).toBeHidden();

  await slideTab.click();

  await expect(page.getByTestId('slide-content')).toBeVisible();
  await expect(page.getByTestId(QA_TABS_TEST_IDS.questionsPanel)).toBeHidden();
});

for (const [name, room] of [
  ['questions are off', qaRoom('off')],
  ['there is no room', null],
] as const) {
  test(`no tab bar, and the slide shows, when ${name}`, async ({ mount, page }) => {
    await stubQuestionsApi(page);
    await mount(
      <GuestQa code="482913" state={live(room)} status="live" presenter="Sam Rivera">
        {slideChild}
      </GuestQa>,
    );
    await expect(page.getByTestId('slide-content')).toBeVisible();
    await expect(page.getByTestId(QA_TABS_TEST_IDS.bar)).toHaveCount(0);
    await expect(page.getByTestId(QA_TABS_TEST_IDS.questionsPanel)).toHaveCount(0);
  });
}

test('in landscape the slide shows and the questions panel hides, even with Questions chosen', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await stubQuestionsApi(page);
  await mount(
    <GuestQa code="482913" state={live(qaRoom('open'))} status="live" presenter="Sam Rivera">
      {slideChild}
    </GuestQa>,
  );
  await page.getByTestId(QA_TABS_TEST_IDS.questionsTab).click();
  await expect(page.getByTestId(QA_TABS_TEST_IDS.questionsPanel)).toBeVisible();

  await page.setViewportSize({ width: 844, height: 390 });

  await expect(page.getByTestId('slide-content')).toBeVisible();
  await expect(page.getByTestId(QA_TABS_TEST_IDS.questionsPanel)).toBeHidden();
  await expect(page.getByTestId(QA_TABS_TEST_IDS.bar)).toBeHidden();
});

test("the slide's content keeps its state when questions go off and on again", async ({
  mount,
  page,
}) => {
  await stubQuestionsApi(page);
  const withRoom = (room: QaRoom) => (
    <GuestQa code="482913" state={live(room)} status="live" presenter="Sam Rivera">
      <input data-testid="child-input" aria-label="Kept" />
    </GuestQa>
  );
  const component = await mount(withRoom(qaRoom('open')));
  await page.getByTestId('child-input').fill('kept');

  await component.update(withRoom(qaRoom('off')));
  await expect(page.getByTestId(QA_TABS_TEST_IDS.bar)).toHaveCount(0);
  await expect(page.getByTestId('child-input')).toHaveValue('kept');

  await component.update(withRoom(qaRoom('open')));
  await expect(page.getByTestId(QA_TABS_TEST_IDS.bar)).toBeVisible();
  await expect(page.getByTestId('child-input')).toHaveValue('kept');
});

test('questions going off while Questions is chosen selects Slide, so they come back on Slide', async ({
  mount,
  page,
}) => {
  await stubQuestionsApi(page);
  const withRoom = (room: QaRoom) => (
    <GuestQa code="482913" state={live(room)} status="live" presenter="Sam Rivera">
      {slideChild}
    </GuestQa>
  );
  const component = await mount(withRoom(qaRoom('open')));
  await page.getByTestId(QA_TABS_TEST_IDS.questionsTab).click();
  await expect(page.getByTestId(QA_TABS_TEST_IDS.questionsTab)).toHaveAttribute(
    'aria-selected',
    'true',
  );

  await component.update(withRoom(qaRoom('off')));
  await expect(page.getByTestId('slide-content')).toBeVisible();
  await component.update(withRoom(qaRoom('open')));

  await expect(page.getByTestId(QA_TABS_TEST_IDS.slideTab)).toHaveAttribute(
    'aria-selected',
    'true',
  );
  await expect(page.getByTestId(QA_TABS_TEST_IDS.questionsPanel)).toBeHidden();
});

test("the questions fixture moves an open question's votes with the toggle, and nothing else", async ({
  mount,
  page,
}) => {
  const view = guestQuestions('view');
  await mount(
    <QuestionsFixture room={view.room} questions={view.questions} mine={view.mine}>
      <PhoneReactionsFixture>
        <QuestionsProbe />
      </PhoneReactionsFixture>
    </QuestionsFixture>,
  );
  const question = (n: number) => page.getByTestId(`probe-question-${n}`);
  await expect(question(1)).toContainText('1 open 14 ');

  // 1 and 2 are open (1 upvoted); 3 is answered; 4 is the guest's own, waiting and upvoted.
  for (const n of [1, 2, 3, 4]) await page.getByTestId(`probe-vote-${n}`).click();

  await expect(question(1)).toContainText('1 open 13 ');
  await expect(question(2)).toContainText('2 open 10 ');
  await expect(question(3)).toContainText('3 answered 6 ');
  await expect(question(4)).toContainText('4 waiting 1 ');
  await expect(page.getByTestId('probe-mine')).toHaveText(
    JSON.stringify({ picks: [2, 4], revision: view.mine.revision }),
  );

  await page.getByTestId('probe-vote-2').click();

  await expect(question(2)).toContainText('2 open 9 ');
});

const ASKING: AskingState[] = ['idle', 'sending', 'queued', 'limited', 'failed'];

for (const asking of ASKING) {
  test(`the questions fixture gives asking ${asking}`, async ({ mount, page }) => {
    await mount(
      <QuestionsFixture
        asking={asking}
        retryIn={asking === 'limited' ? 42 : 0}
        error={asking === 'failed' ? 'questions_closed' : null}
      >
        <PhoneReactionsFixture>
          <QuestionsProbe />
        </PhoneReactionsFixture>
      </QuestionsFixture>,
    );
    await expect(page.getByTestId('probe-asking')).toHaveText(asking);
    await expect(page.getByTestId('probe-retry-in')).toHaveText(asking === 'limited' ? '42' : '0');
    await expect(page.getByTestId('probe-error')).toHaveText(
      asking === 'failed' ? 'questions_closed' : '',
    );
  });
}

test('the fixtures record every call and follow a toggle and a sort', async ({ mount, page }) => {
  const calls: string[] = [];
  const view = guestQuestions('view');
  await mount(
    <QuestionsFixture
      room={view.room}
      questions={view.questions}
      mine={{ picks: [2], revision: 3 }}
      onAsk={(text, name) => calls.push(`ask ${text} ${name}`)}
      onToggleVote={(n) => calls.push(`vote ${n}`)}
      onRetry={() => calls.push('retry')}
      onSort={(sort) => calls.push(`sort ${sort}`)}
    >
      <PhoneReactionsFixture cooling onTap={(emoji) => calls.push(`tap ${emoji}`)}>
        <QuestionsProbe />
      </PhoneReactionsFixture>
    </QuestionsFixture>,
  );

  await page.getByTestId('probe-text').fill('Why?');
  await page.getByTestId('probe-ask').click();
  await page.getByTestId('probe-vote-1').click();
  await page.getByTestId('probe-vote-2').click();
  await page.getByTestId('probe-retry').click();
  await page.getByTestId('probe-sort-toggle').click();
  await page.getByTestId('probe-tap-clap').click();

  await expect(page.getByTestId('probe-mine')).toHaveText(
    JSON.stringify({ picks: [1], revision: 3 }),
  );
  await expect(page.getByTestId('probe-sort')).toHaveText('new');
  await expect(page.getByTestId('probe-cooling')).toHaveText('true');
  await expect(page.getByTestId('probe-state')).toHaveText('ready');
  await expect
    .poll(() => calls)
    .toEqual(['ask Why? null', 'vote 1', 'vote 2', 'retry', 'sort new', 'tap clap']);
});

test('the ask sheet fixture opens the sheet from a prop', async ({ mount, page }) => {
  await mount(
    <QuestionsFixture>
      <AskSheetFixture open>
        <AskSheet presenter="Sam Rivera" />
      </AskSheetFixture>
    </QuestionsFixture>,
  );
  await expect(page.getByTestId(QA_ASK_SHEET_TEST_IDS.sheet)).toBeAttached();
});

// What the page's fetch got back: the status and body (null for an empty one).
const call = (page: Page, method: string, path: string, body: unknown) =>
  page.evaluate(
    async ([m, p, b]) => {
      const response = await fetch(p, {
        method: m,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(b),
      });
      const text = await response.text();
      return { status: response.status, json: text === '' ? null : (JSON.parse(text) as unknown) };
    },
    [method, path, body] as const,
  );

const ask = (page: Page, text: string) =>
  call(page, 'POST', '/api/join/482913/questions', { text, name: null });

test('the stub API refuses an ask while questions are closed, and writes nothing', async ({
  mount,
  page,
}) => {
  const api = await stubQuestionsApi(page, {
    view: { ...guestQuestions('view'), room: qaRoom('closed') },
  });
  await mount(<div />);

  expect(await ask(page, 'Why?')).toEqual({ status: 409, json: { error: 'questions_closed' } });
  expect(api.view.questions).toEqual(guestQuestions('view').questions);
});

test('the stub API refuses a fourth ask with 429 and a retryAfter', async ({ mount, page }) => {
  const api = await stubQuestionsApi(page, { retryAfter: 42 });
  await mount(<div />);

  for (const text of ['One?', 'Two?', 'Three?']) expect((await ask(page, text)).status).toBe(201);
  expect(await ask(page, 'Four?')).toEqual({
    status: 429,
    json: { error: 'too_many_questions', retryAfter: 42 },
  });
  expect(api.view.questions.map((q) => q.number)).toEqual([1, 2, 3]);
});

test('the stub API refuses a stale vote with the current view, and a tap with reactions off', async ({
  mount,
  page,
}) => {
  const view = guestQuestions('view');
  const api = await stubQuestionsApi(page, {
    view: { ...view, room: { ...view.room, reactions: false } },
  });
  await mount(<div />);

  const stale = await call(page, 'PUT', '/api/join/482913/questions/votes', {
    round: view.room.round,
    picks: [1],
    revision: view.mine.revision + 1,
  });
  expect(stale).toEqual({ status: 409, json: { error: 'stale', current: api.view } });
  expect(await call(page, 'POST', '/api/join/482913/reactions', { emoji: 'clap' })).toEqual({
    status: 409,
    json: { error: 'reactions_off' },
  });
  expect(api.taps).toEqual([{ emoji: 'clap' }]);
});
