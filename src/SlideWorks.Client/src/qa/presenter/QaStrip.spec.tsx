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
import { QaControlContext, type QaControlValue } from './useQaControl';
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
    <div style={PAGE}>
      <QaControlFixture deckId={DECK} control={control} error={error} onAction={onAction}>
        <QaStrip deckId={DECK} />
      </QaControlFixture>
    </div>
  );
}

// The remote's real page background and text colour (SpeakerPage's slate-950 and slate-100), so the screenshots show
// what the phone shows: the strip sets no text colour of its own on the votes number.
const PAGE = { background: '#020617', color: '#f1f5f9', minHeight: '100vh' };

// A provider whose actions settle as the test says: `run` is called with the action and the question number and
// answers with the message of that call, or null.
function scripted(
  control: QaControlView,
  run: (action: string, number: number) => Promise<string | null>,
  error: string | null = null,
) {
  const act = (action: string) => (number: number) => run(action, number);
  const none = () => Promise.resolve<string | null>(null);
  const value: QaControlValue = {
    control,
    approve: act('approve'),
    hide: act('hide'),
    restore: act('restore'),
    answer: act('answer'),
    reopen: act('reopen'),
    show: act('show'),
    unshow: act('unshow'),
    setSettings: none,
    setSlide: none,
    error,
  };
  return (
    <div style={PAGE}>
      <QaControlContext value={{ deckId: DECK, slide: null, value }}>
        <QaStrip deckId={DECK} />
      </QaControlContext>
    </div>
  );
}

const textOf = (number: number) => live().questions.find((q) => q.number === number)!.text;

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
    <div style={PAGE}>
      <QaControlLive deckId={DECK} slide={1}>
        <QaStrip deckId={DECK} />
      </QaControlLive>
    </div>,
  );
  await expect(page.getByTestId(IDS.strip)).toBeVisible();
  return api;
}

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

const sheetButton = (
  page: Page,
  action: 'approve' | 'hide' | 'show' | 'unshow' | 'answer',
  n: number,
) => page.getByTestId(qaStripSheetActionTestId(action, n));

test('QaStrip_WhenTheStripIsRead_ShouldNameEachButtonByItsActionAndQuestion', async ({
  mount,
  page,
}) => {
  const view = await mount(strip(live()));
  const named = (name: string) => page.getByRole('button', { name, exact: true });
  await expect(named(`Answered: ${textOf(2)}`)).toHaveAttribute('data-testid', IDS.answer);
  await expect(named(`Take off: ${textOf(2)}`)).toHaveAttribute('data-testid', IDS.takeOff);
  await view.update(strip(offScreen()));
  await expect(named(`Show: ${textOf(1)}`)).toHaveAttribute('data-testid', IDS.show);
  await expect(named(`Answered: ${textOf(1)}`)).toHaveAttribute('data-testid', IDS.answer);
});

test('QaStrip_WhenTheSheetIsRead_ShouldNameEachRowButtonByItsActionAndQuestion', async ({
  mount,
  page,
}) => {
  await mount(strip(live()));
  await page.getByTestId(IDS.all).click();
  const named = (name: string) =>
    page.getByTestId(IDS.sheet).getByRole('button', { name, exact: true });
  await expect(named(`Approve: ${textOf(4)}`)).toHaveAttribute(
    'data-testid',
    qaStripSheetActionTestId('approve', 4),
  );
  await expect(named(`Hide: ${textOf(4)}`)).toHaveAttribute(
    'data-testid',
    qaStripSheetActionTestId('hide', 4),
  );
  await expect(named(`Show: ${textOf(1)}`)).toHaveAttribute(
    'data-testid',
    qaStripSheetActionTestId('show', 1),
  );
  await expect(named(`Answered: ${textOf(1)}`)).toHaveAttribute(
    'data-testid',
    qaStripSheetActionTestId('answer', 1),
  );
  await expect(named(`Take off: ${textOf(2)}`)).toHaveAttribute(
    'data-testid',
    qaStripSheetActionTestId('unshow', 2),
  );
  await expect(named('Close the list of questions')).toHaveAttribute('data-testid', IDS.sheetClose);
  await expect(sheetButton(page, 'answer', 1)).toHaveText('Answered');
  await expect(sheetButton(page, 'unshow', 2)).toHaveText('Take off');
  await expect(sheetButton(page, 'approve', 4)).toHaveText('Approve');
});

test('QaStrip_WhenTheCountsChange_ShouldAnnounceThemInAPoliteStatusRegion', async ({
  mount,
  page,
}) => {
  const view = await mount(strip(live()));
  const region = page.getByRole('status');
  await expect(region).toHaveText('3 open, 1 to review');
  await expect(region).toHaveAttribute('aria-live', 'polite');
  const next = live();
  next.counts = { ...next.counts, open: 2, waiting: 0 };
  await view.update(strip(next));
  await expect(region).toHaveText('2 open, 0 to review');
});

test('QaStrip_WhenTheSheetListsQuestions_ShouldShowVotesWithTheirPluralAndEachQuestionsState', async ({
  mount,
  page,
}) => {
  const view = live();
  view.questions = view.questions.map((q) => (q.number === 6 ? { ...q, votes: 1 } : q));
  await mount(strip(view));
  await page.getByTestId(IDS.all).click();
  const meta = (n: number) => page.getByTestId(qaStripSheetRowTestId(n)).locator('p').nth(1);
  await expect(meta(6)).toHaveText('1 vote');
  await expect(meta(1)).toHaveText('14 votes');
  await expect(meta(2)).toHaveText('9 votes · On screen now');
  await expect(meta(4)).toContainText('· To review');
});

test('QaStrip_WhenTwoQuestionsHaveTheSameVotes_ShouldListTheOlderFirstInTheSheet', async ({
  mount,
  page,
}) => {
  const view = live();
  view.questions = view.questions.map((q) => (q.number === 6 ? { ...q, votes: 14 } : q));
  await mount(strip(view));
  await page.getByTestId(IDS.all).click();
  expect(await rowNumbers(page)).toEqual([4, 1, 6, 2]);
});

test('QaStrip_WhenTheQuestionOnScreenIsHidden_ShouldShowTheTopOpenQuestionInstead', async ({
  mount,
  page,
}) => {
  const view = live();
  view.questions = view.questions.map((q) => (q.number === 2 ? { ...q, hidden: true } : q));
  await mount(strip(view));
  await expect(page.getByTestId(IDS.text)).toHaveText(textOf(1));
  await expect(page.getByTestId(IDS.onScreen)).toHaveCount(0);
});

test('QaStrip_WhenTheSheetIsOpenAndTheDeckGoesNotLiveAndLiveAgain_ShouldStayClosed', async ({
  mount,
  page,
}) => {
  const view = await mount(strip(live()));
  await page.getByTestId(IDS.all).click();
  await expect(page.getByTestId(IDS.sheet)).toBeVisible();
  await view.update(strip(qaControlView('notLive')));
  await expect(page.getByTestId(IDS.strip)).toHaveCount(0);
  await view.update(strip(live()));
  await expect(page.getByTestId(IDS.strip)).toBeVisible();
  await expect(page.getByTestId(IDS.sheet)).toHaveCount(0);
  await expect(page.getByTestId(IDS.all)).toBeVisible();
});

test('QaStrip_WhenTheSheetClosesAfterItsLastQuestionWentThroughIt_ShouldReturnFocusToTheStrip', async ({
  mount,
  page,
}) => {
  const only = offScreen();
  only.questions = only.questions.filter((q) => q.number === 1);
  only.counts = { open: 1, waiting: 0, answered: 0, hidden: 0 };
  await liveStrip(mount, page, { control: only });
  await page.getByTestId(IDS.all).click();
  await sheetButton(page, 'answer', 1).focus();
  await sheetButton(page, 'answer', 1).press('Enter');
  await expect(page.getByTestId(qaStripSheetRowTestId(1))).toHaveCount(0);
  await expect(page.getByTestId(IDS.sheetClose)).toBeFocused();
  await page.getByTestId(IDS.sheetClose).press('Enter');
  await expect(page.getByTestId(IDS.sheet)).toHaveCount(0);
  await expect(page.getByTestId(IDS.all)).toHaveCount(0);
  await expect(page.getByTestId(IDS.strip)).toBeFocused();
});

test('QaStrip_WhenTheSheetClosesAfterEscapeWithAllGone_ShouldReturnFocusToTheStripToo', async ({
  mount,
  page,
}) => {
  const only = offScreen();
  only.questions = only.questions.filter((q) => q.number === 1);
  only.counts = { open: 1, waiting: 0, answered: 0, hidden: 0 };
  await liveStrip(mount, page, { control: only });
  await page.getByTestId(IDS.all).click();
  await sheetButton(page, 'hide', 1).focus();
  await sheetButton(page, 'hide', 1).press('Enter');
  await expect(page.getByTestId(qaStripSheetRowTestId(1))).toHaveCount(0);
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(IDS.sheet)).toHaveCount(0);
  await expect(page.getByTestId(IDS.strip)).toBeFocused();
});

test('QaStrip_WhenARowGoesFromTheSheet_ShouldFocusTheFirstButtonOfTheRowThatTookItsPlace', async ({
  mount,
  page,
}) => {
  await liveStrip(mount, page);
  await page.getByTestId(IDS.all).click();
  expect(await rowNumbers(page)).toEqual([4, 1, 2, 6]);
  await sheetButton(page, 'hide', 4).focus();
  await sheetButton(page, 'hide', 4).press('Enter');
  await expect(page.getByTestId(qaStripSheetRowTestId(4))).toHaveCount(0);
  // Row 1 took row 4's place; it is not on screen, so its first button is Show.
  await expect(sheetButton(page, 'show', 1)).toBeFocused();
});

test('QaStrip_WhenTheLastRowGoesFromTheSheet_ShouldFocusTheRowNowLast', async ({ mount, page }) => {
  await liveStrip(mount, page);
  await page.getByTestId(IDS.all).click();
  await sheetButton(page, 'answer', 6).focus();
  await sheetButton(page, 'answer', 6).press('Enter');
  await expect(page.getByTestId(qaStripSheetRowTestId(6))).toHaveCount(0);
  expect(await rowNumbers(page)).toEqual([4, 1, 2]);
  await expect(sheetButton(page, 'show', 2)).toBeFocused();
});

test('QaStrip_WhenNoRowIsLeftInTheSheet_ShouldFocusClose', async ({ mount, page }) => {
  const only = offScreen();
  only.questions = only.questions.filter((q) => q.number === 1);
  only.counts = { open: 1, waiting: 0, answered: 0, hidden: 0 };
  await liveStrip(mount, page, { control: only });
  await page.getByTestId(IDS.all).click();
  await sheetButton(page, 'answer', 1).focus();
  await sheetButton(page, 'answer', 1).press('Enter');
  await expect(page.getByTestId(IDS.sheetRows)).toContainText('No open or waiting questions.');
  await expect(page.getByTestId(IDS.sheetClose)).toBeFocused();
});

test('QaStrip_WhenASheetActionIsRefusedAndItsButtonStays_ShouldKeepFocusOnThatButton', async ({
  mount,
  page,
}) => {
  await liveStrip(mount, page, {
    routes: { hide: { status: 409, json: { error: 'wrong_state' } } },
  });
  await page.getByTestId(IDS.all).click();
  await sheetButton(page, 'hide', 2).focus();
  await sheetButton(page, 'hide', 2).press('Enter');
  await expect(page.getByTestId(IDS.sheetError)).toContainText(
    'That question had already changed.',
  );
  await expect(sheetButton(page, 'hide', 2)).toBeFocused();
});

test('QaStrip_WhenAnOpenQuestionIsAnsweredFromTheStripAndOthersRemain_ShouldKeepFocusOnTheButton', async ({
  mount,
  page,
}) => {
  await liveStrip(mount, page);
  await page.getByTestId(IDS.answer).focus();
  await page.getByTestId(IDS.answer).press('Enter');
  // Question 1 is answered; the next top question is 2, and the strip is not the focus target: the button is.
  await expect(page.getByTestId(IDS.text)).toHaveText(textOf(2));
  await expect(page.getByTestId(IDS.answer)).toBeFocused();
});

test('QaStrip_WhenShowSettlesAndTheButtonBecomesAnswered_ShouldMoveFocusToTakeOff', async ({
  mount,
  page,
}) => {
  const api = await liveStrip(mount, page);
  await page.getByTestId(IDS.show).focus();
  await page.getByTestId(IDS.show).press('Enter');
  await expect(page.getByTestId(IDS.onScreen)).toBeVisible();
  expect(api.calls.filter((c) => c.call === 'show')).toEqual([{ call: 'show', number: 1 }]);
  // The focused element would read "Answered": a late second tap would mark the question answered.
  await expect(page.getByTestId(IDS.takeOff)).toBeFocused();
  await page.keyboard.press('Enter');
  expect(api.calls.filter((c) => c.call === 'answer')).toEqual([]);
});

test('QaStrip_WhenTakeOffSettlesAndTheButtonBecomesShow_ShouldMoveFocusToShow', async ({
  mount,
  page,
}) => {
  await liveStrip(mount, page, { control: live() });
  await page.getByTestId(IDS.takeOff).focus();
  await page.getByTestId(IDS.takeOff).press('Enter');
  await expect(page.getByTestId(IDS.onScreen)).toHaveCount(0);
  await expect(page.getByTestId(IDS.show)).toBeFocused();
});

test('QaStrip_WhenATapIsStillInFlight_ShouldIgnoreASecondTapAndMarkTheButtonBusy', async ({
  mount,
  page,
}) => {
  const calls: string[] = [];
  const release: Array<(message: string | null) => void> = [];
  await mount(
    scripted(offScreen(), (action, number) => {
      calls.push(`${action} ${number}`);
      return new Promise((resolve) => release.push(resolve));
    }),
  );
  await page.getByTestId(IDS.show).click();
  await expect(page.getByTestId(IDS.show)).toHaveAttribute('aria-busy', 'true');
  await expect(page.getByTestId(IDS.show)).toHaveAttribute('aria-disabled', 'true');
  await page.getByTestId(IDS.answer).evaluate((el) => (el as HTMLElement).click());
  await page.getByTestId(IDS.show).evaluate((el) => (el as HTMLElement).click());
  expect(calls).toEqual(['show 1']);
  release[0](null);
  await expect(page.getByTestId(IDS.show)).not.toHaveAttribute('aria-busy', 'true');
  await expect(page.getByTestId(IDS.show)).not.toHaveAttribute('aria-disabled', 'true');
  await page.getByTestId(IDS.answer).click();
  expect(calls).toEqual(['show 1', 'answer 1']);
});

test('QaStrip_WhenASheetTapIsStillInFlight_ShouldIgnoreASecondTap', async ({ mount, page }) => {
  const calls: string[] = [];
  await mount(
    scripted(live(), (action, number) => {
      calls.push(`${action} ${number}`);
      return new Promise(() => undefined);
    }),
  );
  await page.getByTestId(IDS.all).click();
  await sheetButton(page, 'hide', 2).evaluate((el) => (el as HTMLElement).click());
  await sheetButton(page, 'hide', 2).evaluate((el) => (el as HTMLElement).click());
  await sheetButton(page, 'answer', 2).evaluate((el) => (el as HTMLElement).click());
  expect(calls).toEqual(['hide 2']);
});

test('QaStrip_WhenItsOwnCallIsRefused_ShouldShowThatCallsMessageUnderTheStrip', async ({
  mount,
  page,
}) => {
  const answers = ['That question had already changed.', null];
  await mount(scripted(live(), () => Promise.resolve(answers.shift() ?? null)));
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
  await page.getByTestId(IDS.answer).click();
  await expect(page.getByTestId(IDS.error)).toHaveText('That question had already changed.');
  await expect(page.getByTestId(IDS.error)).toHaveAttribute('role', 'alert');
  // The next call clears it when it succeeds.
  await page.getByTestId(IDS.takeOff).click();
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
});

test('QaStrip_WhenASheetCallIsRefused_ShouldShowThatCallsMessageInTheSheetOnly', async ({
  mount,
  page,
}) => {
  await mount(scripted(live(), () => Promise.resolve('That question had already changed.')));
  await page.getByTestId(IDS.all).click();
  await sheetButton(page, 'hide', 2).click();
  await expect(page.getByTestId(IDS.sheetError)).toHaveText('That question had already changed.');
  await expect(page.getByTestId(IDS.sheetError)).toHaveAttribute('role', 'alert');
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
});

test('QaStrip_WhenAnotherPartsSettingsChangeFailed_ShouldNotShowItsMessageUnderThePhoneStrip', async ({
  mount,
  page,
}) => {
  const settings = "Couldn't change the question settings. Check your connection and try again.";
  await mount(scripted(live(), () => Promise.resolve(null), settings));
  await expect(page.getByTestId(IDS.strip)).toBeVisible();
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
  await expect(page.getByText(settings)).toHaveCount(0);
  await page.getByTestId(IDS.all).click();
  await expect(page.getByTestId(IDS.sheetError)).toHaveCount(0);
  await expect(page.getByText(settings)).toHaveCount(0);
});

test('QaStrip_WhenTheQuestionsCannotBeReadOrWatched_ShouldShowThatFailure', async ({
  mount,
  page,
}) => {
  const watch =
    "Live question updates aren't reaching this window, so this list may be out of date. It keeps trying, and tries again when you change slides.";
  const view = await mount(scripted(live(), () => Promise.resolve(null), watch));
  await expect(page.getByTestId(IDS.error)).toHaveText(watch);
  const read =
    "Couldn't load the questions. Check your connection; they load again when you move or reconnect.";
  await view.update(scripted(live(), () => Promise.resolve(null), read));
  await expect(page.getByTestId(IDS.error)).toHaveText(read);
  await page.getByTestId(IDS.all).click();
  await expect(page.getByTestId(IDS.sheetError)).toHaveText(read);
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
