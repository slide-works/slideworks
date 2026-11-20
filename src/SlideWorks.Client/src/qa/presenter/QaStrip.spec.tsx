import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { guestView } from '../../test-support/fixtures/guestStates';
import { qaControlView } from '../../test-support/fixtures/qaStates';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { stubQaControlApi } from '../../test-support/stubQaControlApi';
import {
  QA_STRIP_TEST_IDS as IDS,
  qaStripSheetActionTestId,
  qaStripSheetRowTestId,
} from '../testIds';
import type { QaControlView } from '../types';
import { QaControlFixture, type QaControlCall } from './QaControlFixture';
import { QaControlLive } from './QaControlLive';
import { QaStrip } from './QaStrip';

// The questions strip on the phone remote under the frozen fixture provider, at 360 px wide: the question on screen or
// the top open one, its buttons, the sheet behind "All n", a refused call's message, and nothing when not live or with
// questions off. The strip in SW-4's real page has QaPresenterSeams.spec.tsx.
// The live sample: open 1 (14 votes), 2 (9, on screen) and 6 (3); waiting 4; answered 3; hidden 5; review on.

test.use({ viewport: { width: 360, height: 640 } });

const DECK = 'deck-1';

const live = (overrides: Partial<QaControlView> = {}) => qaControlView('live', overrides);

// The live sample with nothing on screen.
function offScreen(): QaControlView {
  const view = live({ onScreen: null });
  view.questions = view.questions.map((q) => ({ ...q, onScreen: false }));
  return view;
}

function strip(
  control: QaControlView | null,
  { error = null, onAction }: { error?: string | null; onAction?: (c: QaControlCall) => void } = {},
) {
  return (
    <QaControlFixture deckId={DECK} control={control} error={error} onAction={onAction}>
      <QaStrip deckId={DECK} />
    </QaControlFixture>
  );
}

const rowNumbers = (page: Page) =>
  page
    .getByTestId(IDS.sheetRows)
    .locator('[data-number]')
    .evaluateAll((rows) => rows.map((row) => Number((row as HTMLElement).dataset.number)));

test('QaStrip_WhenAQuestionIsOnScreen_ShouldShowItWithItsPillVotesAndCounts', async ({
  mount,
  page,
}) => {
  await mount(strip(live()));
  await expect(page.getByTestId(IDS.onScreen)).toHaveText('On screen');
  await expect(page.getByTestId(IDS.text)).toHaveText(
    'What is the smallest slice you have shipped?',
  );
  await expect(page.getByTestId(IDS.votes)).toHaveText('9');
  await expect(page.getByTestId(IDS.counts)).toHaveText('votes · 3 open · 1 to review');
  await expect(page.getByTestId(IDS.answer)).toHaveText('Answered');
  await expect(page.getByTestId(IDS.takeOff)).toHaveText('Take off');
  await expect(page.getByTestId(IDS.show)).toHaveCount(0);
  await expect(page.getByTestId(IDS.all)).toHaveText('All 4');
});

test('QaStrip_WhenNothingIsOnScreen_ShouldShowTheTopOpenQuestionWithShowAndAnswered', async ({
  mount,
  page,
}) => {
  await mount(strip(offScreen()));
  await expect(page.getByTestId(IDS.onScreen)).toHaveCount(0);
  await expect(page.getByTestId(IDS.text)).toHaveText('How do you split a big change?');
  await expect(page.getByTestId(IDS.votes)).toHaveText('14');
  await expect(page.getByTestId(IDS.show)).toHaveText('Show');
  await expect(page.getByTestId(IDS.answer)).toHaveText('Answered');
  await expect(page.getByTestId(IDS.takeOff)).toHaveCount(0);
});

test('QaStrip_WhenTwoOpenQuestionsTie_ShouldShowTheOlderOne', async ({ mount, page }) => {
  const view = offScreen();
  view.questions = view.questions.map((q) => (q.number === 6 ? { ...q, votes: 14 } : q));
  await mount(strip(view));
  await expect(page.getByTestId(IDS.text)).toHaveText('How do you split a big change?');
});

test('QaStrip_WhenHiddenAndWaitingQuestionsHaveMoreVotes_ShouldNotShowThem', async ({
  mount,
  page,
}) => {
  const view = offScreen();
  view.questions = view.questions.map((q) =>
    q.number === 4 || q.number === 5 || q.number === 3 ? { ...q, votes: 99 } : q,
  );
  await mount(strip(view));
  await expect(page.getByTestId(IDS.text)).toHaveText('How do you split a big change?');
});

test('QaStrip_WhenThereAreNoOpenQuestions_ShouldSaySoAndKeepTheToReviewCount', async ({
  mount,
  page,
}) => {
  const view = offScreen();
  view.questions = view.questions.map((q) =>
    q.state === 'open' ? { ...q, state: 'answered' } : q,
  );
  view.counts = { open: 0, waiting: 1, answered: 3, hidden: 0 };
  await mount(strip(view));
  await expect(page.getByTestId(IDS.empty)).toContainText('No open questions');
  await expect(page.getByTestId(IDS.counts)).toHaveText(' · 1 to review');
  await expect(page.getByTestId(IDS.text)).toHaveCount(0);
  await expect(page.getByTestId(IDS.show)).toHaveCount(0);
  await expect(page.getByTestId(IDS.answer)).toHaveCount(0);
  await expect(page.getByTestId(IDS.all)).toHaveText('All 1');
});

test('QaStrip_WhenNothingIsOpenOrWaiting_ShouldOfferNoSheet', async ({ mount, page }) => {
  const view = offScreen();
  view.questions = view.questions.filter((q) => q.state === 'answered');
  view.counts = { open: 0, waiting: 0, answered: 1, hidden: 0 };
  await mount(strip(view));
  await expect(page.getByTestId(IDS.empty)).toBeVisible();
  await expect(page.getByTestId(IDS.counts)).toHaveCount(0);
  await expect(page.getByTestId(IDS.all)).toHaveCount(0);
});

test('QaStrip_WhenQuestionsAreWaitingForReview_ShouldCountThemAndLeaveTheCountOutWhenNone', async ({
  mount,
  page,
}) => {
  const view = await mount(strip(live()));
  await expect(page.getByTestId(IDS.counts)).toHaveText('votes · 3 open · 1 to review');
  const none = live();
  none.counts = { ...none.counts, waiting: 0 };
  await view.update(strip(none));
  await expect(page.getByTestId(IDS.counts)).toHaveText('votes · 3 open');
});

test('QaStrip_WhenAButtonIsPressed_ShouldCallItsActionOnTheShownQuestion', async ({
  mount,
  page,
}) => {
  const calls: QaControlCall[] = [];
  const view = await mount(strip(offScreen(), { onAction: (c) => calls.push(c) }));
  await page.getByTestId(IDS.show).click();
  await page.getByTestId(IDS.answer).click();
  await expect.poll(() => calls.length).toBe(2);
  expect(calls).toEqual([
    { action: 'show', number: 1 },
    { action: 'answer', number: 1 },
  ]);
  await view.update(strip(live(), { onAction: (c) => calls.push(c) }));
  await page.getByTestId(IDS.takeOff).click();
  await page.getByTestId(IDS.answer).click();
  await expect.poll(() => calls.length).toBe(4);
  expect(calls.slice(2)).toEqual([
    { action: 'unshow', number: 2 },
    { action: 'answer', number: 2 },
  ]);
});

test('QaStrip_WhenACallWasRefused_ShouldShowItsMessageAsAnAlert', async ({ mount, page }) => {
  const view = await mount(strip(live()));
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
  await view.update(strip(live(), { error: 'That question had already changed.' }));
  await expect(page.getByTestId(IDS.error)).toHaveText('That question had already changed.');
  await expect(page.getByTestId(IDS.error)).toHaveAttribute('role', 'alert');
});

test('QaStrip_WhenAllIsPressed_ShouldListTheOpenAndToReviewQuestionsOnly', async ({
  mount,
  page,
}) => {
  await mount(strip(live()));
  await expect(page.getByTestId(IDS.sheet)).toHaveCount(0);
  await page.getByTestId(IDS.all).click();
  await expect(page.getByTestId(IDS.sheet)).toBeVisible();
  // To review first, then the open ones by votes: not answered 3, not hidden 5.
  expect(await rowNumbers(page)).toEqual([4, 1, 2, 6]);
  await expect(page.getByTestId(qaStripSheetRowTestId(2))).toContainText('On screen now');
});

test('QaStrip_WhenTheSheetRowsArePressed_ShouldCallEachRowsAction', async ({ mount, page }) => {
  const calls: QaControlCall[] = [];
  await mount(strip(live(), { onAction: (c) => calls.push(c) }));
  await page.getByTestId(IDS.all).click();
  const press = (action: 'approve' | 'hide' | 'show' | 'unshow' | 'answer', number: number) =>
    page.getByTestId(qaStripSheetActionTestId(action, number)).click();
  await press('approve', 4);
  await press('hide', 4);
  await press('show', 1);
  await press('answer', 6);
  await press('unshow', 2);
  await expect.poll(() => calls.length).toBe(5);
  expect(calls).toEqual([
    { action: 'approve', number: 4 },
    { action: 'hide', number: 4 },
    { action: 'show', number: 1 },
    { action: 'answer', number: 6 },
    { action: 'unshow', number: 2 },
  ]);
  // A waiting question has no Show or Answered; an open one has no Approve.
  await expect(page.getByTestId(qaStripSheetActionTestId('show', 4))).toHaveCount(0);
  await expect(page.getByTestId(qaStripSheetActionTestId('answer', 4))).toHaveCount(0);
  await expect(page.getByTestId(qaStripSheetActionTestId('approve', 1))).toHaveCount(0);
});

test('QaStrip_WhenTheSheetIsOpenAndACallWasRefused_ShouldShowTheMessageInTheSheetOnly', async ({
  mount,
  page,
}) => {
  const view = await mount(strip(live()));
  await page.getByTestId(IDS.all).click();
  await view.update(strip(live(), { error: 'That question had already changed.' }));
  await expect(page.getByTestId(IDS.sheetError)).toHaveText('That question had already changed.');
  await expect(page.getByTestId(IDS.sheetError)).toHaveAttribute('role', 'alert');
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
});

test('QaStrip_WhenTheSheetIsClosed_ShouldReturnFocusToAll', async ({ mount, page }) => {
  await mount(strip(live()));
  await page.getByTestId(IDS.all).click();
  await page.getByTestId(IDS.sheetClose).click();
  await expect(page.getByTestId(IDS.sheet)).toHaveCount(0);
  await expect(page.getByTestId(IDS.all)).toBeFocused();
  await page.getByTestId(IDS.all).click();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(IDS.sheet)).toHaveCount(0);
  await expect(page.getByTestId(IDS.all)).toBeFocused();
});

test('QaStrip_WhenNotLiveOrQuestionsAreOff_ShouldRenderNothing', async ({ mount, page }) => {
  const view = await mount(strip(null));
  await expect(page.locator('#root')).toBeEmpty();
  await view.update(strip(qaControlView('notLive')));
  await expect(page.getByTestId(IDS.strip)).toHaveCount(0);
  const noRound = live({ round: null });
  await view.update(strip(noRound));
  await expect(page.getByTestId(IDS.strip)).toHaveCount(0);
  const off = live();
  off.settings = { ...off.settings, questions: 'off' };
  await view.update(strip(off));
  await expect(page.getByTestId(IDS.strip)).toHaveCount(0);
  await view.update(strip(live()));
  await expect(page.getByTestId(IDS.strip)).toHaveCount(1);
});

test('QaStrip_WhenTheQuestionIsLong_ShouldShowItOnTwoLinesAtMost', async ({ mount, page }) => {
  const view = offScreen();
  view.questions = view.questions.map((q) =>
    q.number === 1 ? { ...q, text: 'A very long question about splitting work. '.repeat(12) } : q,
  );
  await mount(strip(view));
  const box = await page.getByTestId(IDS.text).evaluate((el) => {
    const style = getComputedStyle(el);
    return { height: el.getBoundingClientRect().height, line: parseFloat(style.lineHeight) };
  });
  expect(box.height).toBeLessThanOrEqual(box.line * 2 + 1);
});

test('QaStrip_At360px_ShouldKeepEveryTargetAtLeast44pxAndNotScrollSideways', async ({
  mount,
  page,
}) => {
  await mount(strip(live()));
  const buttons = [IDS.answer, IDS.takeOff, IDS.all];
  for (const id of buttons) {
    const box = (await page.getByTestId(id).boundingBox())!;
    expect(box.height, id).toBeGreaterThanOrEqual(44);
    expect(box.width, id).toBeGreaterThanOrEqual(44);
  }
  await page.getByTestId(IDS.all).click();
  for (const [action, number] of [
    ['approve', 4],
    ['hide', 4],
    ['show', 1],
    ['answer', 1],
    ['unshow', 2],
  ] as const) {
    const box = (await page.getByTestId(qaStripSheetActionTestId(action, number)).boundingBox())!;
    expect(box.height, `${action} ${number}`).toBeGreaterThanOrEqual(44);
    expect(box.width, `${action} ${number}`).toBeGreaterThanOrEqual(44);
  }
  const box = (await page.getByTestId(IDS.sheetClose).boundingBox())!;
  expect(box.height).toBeGreaterThanOrEqual(44);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
});

// The real provider over the stub hub and the stub routes: what the presenter sees when a call really settles or is refused.
async function liveStrip(
  mount: (component: React.JSX.Element) => Promise<unknown>,
  page: Page,
  options: Parameters<typeof stubQaControlApi>[2] = {},
) {
  await stubDeckHub(page, { audience: { state: guestView('live').state } });
  const api = await stubQaControlApi(page, DECK, { control: offScreen(), ...options });
  await mount(
    <QaControlLive deckId={DECK} slide={1}>
      <QaStrip deckId={DECK} />
    </QaControlLive>,
  );
  await expect(page.getByTestId(IDS.strip)).toBeVisible();
  return api;
}

test('QaStrip_WhenShowSettlesAndTheButtonBecomesTakeOff_ShouldKeepFocusInTheStrip', async ({
  mount,
  page,
}) => {
  const api = await liveStrip(mount, page);
  await page.getByTestId(IDS.show).focus();
  await page.getByTestId(IDS.show).press('Enter');
  await expect(page.getByTestId(IDS.onScreen)).toBeVisible();
  expect(api.calls.filter((c) => c.call === 'show')).toEqual([{ call: 'show', number: 1 }]);
  await expect(page.getByTestId(IDS.show)).toHaveCount(0);
  // The same button now reads Answered: focus stayed in the strip, on a button.
  expect(
    await page.evaluate(() => document.activeElement?.closest('[data-testid="qa-strip"]') !== null),
  ).toBe(true);
});

test('QaStrip_WhenAnsweringTheLastOpenQuestionRemovesTheButtons_ShouldMoveFocusToTheStrip', async ({
  mount,
  page,
}) => {
  const only = offScreen();
  only.questions = only.questions.filter((q) => q.number === 1);
  only.counts = { open: 1, waiting: 0, answered: 0, hidden: 0 };
  await liveStrip(mount, page, { control: only });
  await page.getByTestId(IDS.answer).focus();
  await page.getByTestId(IDS.answer).press('Enter');
  await expect(page.getByTestId(IDS.empty)).toBeVisible();
  await expect(page.getByTestId(IDS.strip)).toBeFocused();
});

test('QaStrip_WhenTheServerRefusesAnAction_ShouldShowTheProvidersMessageAndChangeNothing', async ({
  mount,
  page,
}) => {
  await liveStrip(mount, page, {
    routes: { show: { status: 409, json: { error: 'wrong_state' } } },
  });
  await page.getByTestId(IDS.show).click();
  await expect(page.getByTestId(IDS.error)).toContainText('That question had already changed.');
  await expect(page.getByTestId(IDS.onScreen)).toHaveCount(0);
  await expect(page.getByTestId(IDS.show)).toBeVisible();
});

test('QaStrip_WhenASheetActionSettlesAndItsRowGoes_ShouldKeepFocusInTheSheet', async ({
  mount,
  page,
}) => {
  await liveStrip(mount, page);
  await page.getByTestId(IDS.all).click();
  await page.getByTestId(qaStripSheetActionTestId('approve', 4)).focus();
  await page.getByTestId(qaStripSheetActionTestId('approve', 4)).press('Enter');
  // 4 is open now: its buttons are Show, Answered, Hide, and focus is on one of them.
  await expect(page.getByTestId(qaStripSheetActionTestId('show', 4))).toBeVisible();
  await page.getByTestId(qaStripSheetActionTestId('hide', 4)).press('Enter');
  await expect(page.getByTestId(qaStripSheetRowTestId(4))).toHaveCount(0);
  expect(
    await page.evaluate(
      () => document.activeElement?.closest('[data-testid="qa-strip-sheet"]') !== null,
    ),
  ).toBe(true);
});

test('screenshot QaStrip on screen', async ({ mount, page }) => {
  await mount(strip(live()));
  await expect(page.getByTestId(IDS.strip)).toHaveScreenshot('qa-strip-on-screen.png');
});

test('screenshot QaStrip top open question', async ({ mount, page }) => {
  await mount(strip(offScreen()));
  await expect(page.getByTestId(IDS.strip)).toHaveScreenshot('qa-strip-top.png');
});

test('screenshot QaStrip no open questions', async ({ mount, page }) => {
  const view = offScreen();
  view.questions = view.questions.filter((q) => q.state === 'answered');
  view.counts = { open: 0, waiting: 0, answered: 1, hidden: 0 };
  await mount(strip(view));
  await expect(page.getByTestId(IDS.strip)).toHaveScreenshot('qa-strip-empty.png');
});

test('screenshot QaStrip sheet', async ({ mount, page }) => {
  await mount(strip(live()));
  await page.getByTestId(IDS.all).click();
  await expect(page).toHaveScreenshot('qa-strip-sheet.png');
});
