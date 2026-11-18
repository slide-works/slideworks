import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { qaControlView } from '../../test-support/fixtures/qaStates';
import {
  QA_PANEL_TEST_IDS as IDS,
  qaPanelActionTestId,
  qaPanelReactionCountTestId,
  qaPanelRowMetaTestId,
  qaPanelRowTestId,
  qaPanelRowTextTestId,
  qaPanelRowVotesTestId,
} from '../testIds';
import type { QaAction, QaControlView } from '../types';
import { QaControlFixture, type QaControlCall } from './QaControlFixture';
import { QaPanel } from './QaPanel';

// The Questions panel under the frozen fixture provider: each tab's rows and buttons by state, the review tab's rule,
// the sort, the empty, closed and not-live texts, the counter and a refused call's message. The real provider has its
// own spec (QaControlLive.spec.tsx); the panel in SW-4's presenter window has QaPresenterSeams.spec.tsx.
// The live sample: open 1 (14 votes), 2 (9, on screen) and 6 (3); waiting 4; answered 3; hidden 5; review on.

const DECK = 'deck-1';

const live = (overrides: Partial<QaControlView> = {}) => qaControlView('live', overrides);

function panel(
  control: QaControlView | null,
  { error = null, onAction }: { error?: string | null; onAction?: (c: QaControlCall) => void } = {},
) {
  return (
    <QaControlFixture deckId={DECK} control={control} error={error} onAction={onAction}>
      <QaPanel deckId={DECK} />
    </QaControlFixture>
  );
}

const rowNumbers = (page: Page) =>
  page
    .getByTestId(IDS.rows)
    .locator('[data-number]')
    .evaluateAll((rows) => rows.map((row) => Number((row as HTMLElement).dataset.number)));

// The action buttons a row shows, in order.
const buttonsOf = (page: Page, number: number) =>
  page
    .getByTestId(qaPanelRowTestId(number))
    .locator('button')
    .evaluateAll((buttons) => buttons.map((b) => b.textContent));

test('QaPanel_WhenLive_ShouldShowTheFourTabsWithTheirCountsAndTheOpenOnesRows', async ({
  mount,
  page,
}) => {
  await mount(panel(live()));
  await expect(page.getByTestId(IDS.tabOpen)).toHaveText('Open 3');
  await expect(page.getByTestId(IDS.tabReview)).toHaveText('To review 1');
  await expect(page.getByTestId(IDS.tabAnswered)).toHaveText('Answered 1');
  await expect(page.getByTestId(IDS.tabHidden)).toHaveText('Hidden 1');
  await expect(page.getByTestId(IDS.tabOpen)).toHaveAttribute('aria-selected', 'true');
  expect(await rowNumbers(page)).toEqual([1, 2, 6]);

  await expect(page.getByTestId(qaPanelRowTextTestId(1))).toHaveText(
    'How do you split a big change?',
  );
  await expect(page.getByTestId(qaPanelRowVotesTestId(1))).toContainText('14');
  await expect(page.getByTestId(qaPanelRowMetaTestId(1))).toHaveText('Priya · 12 min');
  await expect(page.getByTestId(qaPanelRowMetaTestId(6))).toHaveText('Anonymous · now');
  await expect(page.getByTestId(qaPanelRowMetaTestId(2))).toHaveText(
    'Anonymous · 9 min · On screen now',
  );
  await expect(page.getByTestId(qaPanelRowTestId(2))).toBeVisible();
});

test('QaPanel_WhenEachTabIsChosen_ShouldShowItsRowsAndTheButtonsItsStateAllows', async ({
  mount,
  page,
}) => {
  await mount(panel(live()));
  expect(await buttonsOf(page, 1)).toEqual(['Show on screen', 'Answered', 'Hide']);
  expect(await buttonsOf(page, 2)).toEqual(['Take off screen', 'Answered', 'Hide']);

  await page.getByTestId(IDS.tabReview).click();
  expect(await rowNumbers(page)).toEqual([4]);
  expect(await buttonsOf(page, 4)).toEqual(['Approve', 'Hide']);

  await page.getByTestId(IDS.tabAnswered).click();
  expect(await rowNumbers(page)).toEqual([3]);
  expect(await buttonsOf(page, 3)).toEqual(['Reopen']);

  await page.getByTestId(IDS.tabHidden).click();
  expect(await rowNumbers(page)).toEqual([5]);
  expect(await buttonsOf(page, 5)).toEqual(['Restore']);
});

test('QaPanel_WhenAButtonIsPressed_ShouldCallItsActionWithTheQuestionNumber', async ({
  mount,
  page,
}) => {
  const calls: QaControlCall[] = [];
  await mount(panel(live(), { onAction: (c) => calls.push(c) }));

  const press = async (action: QaAction, number: number) =>
    page.getByTestId(qaPanelActionTestId(action, number)).click();
  await press('show', 1);
  await press('unshow', 2);
  await press('answer', 6);
  await press('hide', 1);
  await page.getByTestId(IDS.tabReview).click();
  await press('approve', 4);
  await press('hide', 4);
  await page.getByTestId(IDS.tabAnswered).click();
  await press('reopen', 3);
  await page.getByTestId(IDS.tabHidden).click();
  await press('restore', 5);

  expect(calls).toEqual([
    { action: 'show', number: 1 },
    { action: 'unshow', number: 2 },
    { action: 'answer', number: 6 },
    { action: 'hide', number: 1 },
    { action: 'approve', number: 4 },
    { action: 'hide', number: 4 },
    { action: 'reopen', number: 3 },
    { action: 'restore', number: 5 },
  ]);
});

test('QaPanel_WhenTheDeckShowsOnTheQaSlide_ShouldNameTheShowButtonForIt', async ({
  mount,
  page,
}) => {
  const base = live();
  await mount(panel({ ...base, settings: { ...base.settings, showOn: 'qaSlide' } }));
  expect(await buttonsOf(page, 1)).toEqual(['Show on Q&A slide', 'Answered', 'Hide']);
  await expect(page.getByTestId(qaPanelActionTestId('show', 1))).toHaveAccessibleName(
    'Show on Q&A slide: How do you split a big change?',
  );
});

test('QaPanel_WhenTheSortChanges_ShouldOrderTheRowsByTopOrNewestAndKeepItOverTabs', async ({
  mount,
  page,
}) => {
  await mount(panel(live()));
  await expect(page.getByTestId(IDS.sort)).toHaveText('Top ▾');
  expect(await rowNumbers(page)).toEqual([1, 2, 6]);

  await page.getByTestId(IDS.sort).click();
  await expect(page.getByTestId(IDS.sortTop)).toHaveAttribute('aria-checked', 'true');
  await expect(page.getByTestId(IDS.sortTop)).toBeFocused();
  await page.getByTestId(IDS.sortNewest).click();
  await expect(page.getByTestId(IDS.sort)).toHaveText('Newest ▾');
  await expect(page.getByTestId(IDS.sortTop)).toHaveCount(0);
  expect(await rowNumbers(page)).toEqual([6, 2, 1]);
  await expect(page.getByTestId(IDS.sort)).toBeFocused();

  await page.getByTestId(IDS.tabAnswered).click();
  await page.getByTestId(IDS.tabOpen).click();
  expect(await rowNumbers(page)).toEqual([6, 2, 1]);
});

test('QaPanel_WhenTheSortMenuIsOpen_ShouldCloseOnEscapeWithoutChangingTheSortOrMovingTheTalk', async ({
  mount,
  page,
}) => {
  await mount(panel(live()));
  await page.evaluate(() => {
    window.addEventListener('keydown', (e) => {
      if (!e.defaultPrevented) (window as unknown as { moved?: number }).moved = 1;
    });
  });
  await page.getByTestId(IDS.sort).click();
  await page.keyboard.press('ArrowDown');
  await expect(page.getByTestId(IDS.sortNewest)).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(IDS.sortNewest)).toHaveCount(0);
  await expect(page.getByTestId(IDS.sort)).toBeFocused();
  await expect(page.getByTestId(IDS.sort)).toHaveText('Top ▾');
  const moved = (await page.evaluate(() => (window as unknown as { moved?: number }).moved)) ?? 0;
  expect(moved).toBe(0);
});

test('QaPanel_WhenReviewIsOffAndNothingWaits_ShouldNotShowTheReviewTab', async ({
  mount,
  page,
}) => {
  const base = live();
  const none = {
    ...base,
    settings: { ...base.settings, review: false },
    counts: { ...base.counts, waiting: 0 },
    questions: base.questions.filter((q) => q.state !== 'waiting'),
  };
  const view = await mount(panel(none));
  await expect(page.getByTestId(IDS.tabOpen)).toBeVisible();
  await expect(page.getByTestId(IDS.tabReview)).toHaveCount(0);

  // Review on, nothing waiting: the tab shows, unmarked, with its own empty text.
  await view.update(panel({ ...none, settings: { ...none.settings, review: true } }));
  await expect(page.getByTestId(IDS.tabReview)).toHaveText('To review 0');
  await expect(page.getByTestId(IDS.tabReviewMark)).toHaveCount(0);
  await page.getByTestId(IDS.tabReview).click();
  await expect(page.getByTestId(IDS.emptyReview)).toHaveText('Nothing waiting for approval.');

  // Review off with something waiting: the tab shows, marked.
  await view.update(panel({ ...base, settings: { ...base.settings, review: false } }));
  await expect(page.getByTestId(IDS.tabReview)).toHaveText('To review 1');
  await expect(page.getByTestId(IDS.tabReviewMark)).toBeVisible();
});

test('QaPanel_WhenTheReviewTabGoesAwayWhileChosen_ShouldFallBackToOpen', async ({
  mount,
  page,
}) => {
  const base = live();
  const view = await mount(panel(base));
  await page.getByTestId(IDS.tabReview).click();
  await expect(page.getByTestId(IDS.tabReview)).toHaveAttribute('aria-selected', 'true');

  await view.update(
    panel({
      ...base,
      settings: { ...base.settings, review: false },
      counts: { ...base.counts, waiting: 0 },
      questions: base.questions.filter((q) => q.state !== 'waiting'),
    }),
  );
  await expect(page.getByTestId(IDS.tabReview)).toHaveCount(0);
  await expect(page.getByTestId(IDS.tabOpen)).toHaveAttribute('aria-selected', 'true');
  expect(await rowNumbers(page)).toEqual([1, 2, 6]);
});

test('QaPanel_WhenATabIsEmpty_ShouldSayWhyInItsOwnWords', async ({ mount, page }) => {
  const base = live();
  const empty = {
    ...base,
    onScreen: null,
    questions: [],
    counts: { open: 0, waiting: 0, answered: 0, hidden: 0 },
  };
  const view = await mount(panel(empty));
  await expect(page.getByTestId(IDS.emptyOpen)).toHaveText(
    'No open questions. They appear here as the room asks.',
  );
  await page.getByTestId(IDS.tabReview).click();
  await expect(page.getByTestId(IDS.emptyReview)).toHaveText('Nothing waiting for approval.');
  await page.getByTestId(IDS.tabAnswered).click();
  await expect(page.getByTestId(IDS.emptyAnswered)).toHaveText('Nothing answered yet.');
  await page.getByTestId(IDS.tabHidden).click();
  await expect(page.getByTestId(IDS.emptyHidden)).toHaveText('Nothing hidden.');
  await expect(page.getByTestId(IDS.rows)).toHaveCount(0);

  // Closed: the Open tab says so, and the other tabs keep their own texts.
  await page.getByTestId(IDS.tabOpen).click();
  await view.update(panel({ ...empty, settings: { ...empty.settings, questions: 'closed' } }));
  await expect(page.getByTestId(IDS.closed)).toHaveText(
    'Questions are closed. Nothing new can be asked.',
  );
  await expect(page.getByTestId(IDS.emptyOpen)).toHaveCount(0);
});

test('QaPanel_WhenClosedWithQuestionsStillOpen_ShouldKeepShowingThem', async ({ mount, page }) => {
  const base = live();
  await mount(panel({ ...base, settings: { ...base.settings, questions: 'closed' } }));
  expect(await rowNumbers(page)).toEqual([1, 2, 6]);
  await expect(page.getByTestId(IDS.closed)).toHaveCount(0);
});

test('QaPanel_WhenNotLive_ShouldSayQuestionsStartWhenYouGoLiveAndShowNoTabsOrRows', async ({
  mount,
  page,
}) => {
  const view = await mount(panel(qaControlView('notLive')));
  await expect(page.getByTestId(IDS.notLive)).toHaveText('Questions start when you go live.');
  await expect(page.getByTestId(IDS.tabs)).toHaveCount(0);
  await expect(page.getByTestId(IDS.rows)).toHaveCount(0);
  await expect(page.getByTestId(IDS.panel)).toBeVisible();

  // Not read yet: nothing to say.
  await view.update(panel(null));
  await expect(page.getByTestId(IDS.notLive)).toHaveCount(0);
  await expect(page.getByTestId(IDS.tabs)).toHaveCount(0);
  await expect(page.getByTestId(IDS.counter)).toHaveCount(0);
});

test('QaPanel_WhenQuestionsAreOff_ShouldShowNoTabsOrRows', async ({ mount, page }) => {
  const base = live();
  await mount(
    panel({
      ...base,
      round: null,
      questions: [],
      onScreen: null,
      counts: { open: 0, waiting: 0, answered: 0, hidden: 0 },
      settings: { ...base.settings, questions: 'off' },
    }),
  );
  await expect(page.getByTestId(IDS.panel)).toContainText('Questions are off for this deck.');
  await expect(page.getByTestId(IDS.tabs)).toHaveCount(0);
  await expect(page.getByTestId(IDS.rows)).toHaveCount(0);
  await expect(page.getByTestId(IDS.notLive)).toHaveCount(0);
  await expect(page.getByTestId(IDS.counter)).toBeVisible();
});

test('QaPanel_WhenAHiddenQuestionWasOnScreen_ShouldNotSayItIsOnScreen', async ({ mount, page }) => {
  const base = live();
  await mount(
    panel({
      ...base,
      questions: base.questions.map((q) => (q.number === 5 ? { ...q, onScreen: true } : q)),
    }),
  );
  await page.getByTestId(IDS.tabHidden).click();
  await expect(page.getByTestId(qaPanelRowMetaTestId(5))).not.toContainText('On screen now');
});

test('QaPanel_WhenTheCounterHasReactions_ShouldShowEachSentEmojiWithItsTotal', async ({
  mount,
  page,
}) => {
  const base = live();
  const view = await mount(
    panel({ ...base, reactions: { thumbsUp: 3, heart: 1, laugh: 0, wow: 0, clap: 0 } }),
  );
  await expect(page.getByTestId(qaPanelReactionCountTestId('thumbsUp'))).toHaveText('👍 3');
  await expect(page.getByTestId(qaPanelReactionCountTestId('heart'))).toHaveText('❤️ 1');
  await expect(page.getByTestId(qaPanelReactionCountTestId('laugh'))).toHaveCount(0);
  await expect(page.getByTestId(IDS.counterEmpty)).toHaveCount(0);

  await view.update(
    panel({ ...base, reactions: { thumbsUp: 0, heart: 0, laugh: 0, wow: 0, clap: 0 } }),
  );
  await expect(page.getByTestId(IDS.counterEmpty)).toHaveText('No reactions yet');

  await view.update(panel({ ...base, settings: { ...base.settings, reactions: false } }));
  await expect(page.getByTestId(IDS.counterOff)).toHaveText('Reactions off');
  await expect(page.getByTestId(qaPanelReactionCountTestId('thumbsUp'))).toHaveCount(0);
});

test('QaPanel_WhenTheCounterChanges_ShouldKeepTheTalkingNumbersOutOfALiveRegionExceptTheCalmOne', async ({
  mount,
  page,
}) => {
  await mount(panel(live()));
  // Only the counter's own polite region announces; the visible counts are hidden from a screen reader.
  await expect(page.getByTestId(IDS.counter).locator('[aria-live]')).toHaveCount(1);
  await expect(page.getByTestId(IDS.counter).locator('[aria-hidden="true"]')).toHaveCount(1);
});

test('QaPanel_WhenACallWasRefused_ShouldShowItsMessageAsAnAlert', async ({ mount, page }) => {
  const view = await mount(panel(live()));
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
  await view.update(panel(live(), { error: 'That question had already changed.' }));
  await expect(page.getByTestId(IDS.error)).toHaveText('That question had already changed.');
  await expect(page.getByTestId(IDS.error)).toHaveAttribute('role', 'alert');
});

test('QaPanel_WhenTheTabsHaveFocus_ShouldMoveWithArrowKeysWithoutLettingThePresenterWindowSeeThem', async ({
  mount,
  page,
}) => {
  await mount(panel(live()));
  await page.evaluate(() => {
    window.addEventListener('keydown', (e) => {
      if (!e.defaultPrevented) (window as unknown as { leaked?: string }).leaked = e.key;
    });
  });
  await page.getByTestId(IDS.tabOpen).focus();
  await page.keyboard.press('ArrowRight');
  await expect(page.getByTestId(IDS.tabReview)).toBeFocused();
  await expect(page.getByTestId(IDS.tabReview)).toHaveAttribute('aria-selected', 'true');
  await page.keyboard.press('End');
  await expect(page.getByTestId(IDS.tabHidden)).toBeFocused();
  await page.keyboard.press('Home');
  await expect(page.getByTestId(IDS.tabOpen)).toBeFocused();
  await page.keyboard.press('ArrowLeft');
  await expect(page.getByTestId(IDS.tabHidden)).toBeFocused();
  expect(await page.evaluate(() => (window as unknown as { leaked?: string }).leaked)).toBe(
    undefined,
  );
});

test('QaPanel every test id resolves in the live sample', async ({ mount, page }) => {
  await mount(panel(live(), { error: 'Something failed.' }));
  const always = [
    IDS.panel,
    IDS.tabs,
    IDS.tabOpen,
    IDS.tabReview,
    IDS.tabReviewMark,
    IDS.tabAnswered,
    IDS.tabHidden,
    IDS.sort,
    IDS.rows,
    IDS.error,
    IDS.counter,
  ];
  for (const id of always) await expect(page.getByTestId(id)).toBeVisible();
  await page.getByTestId(IDS.sort).click();
  for (const id of [IDS.sortTop, IDS.sortNewest]) await expect(page.getByTestId(id)).toBeVisible();
});

test('QaPanel every state text id resolves in its state', async ({ mount, page }) => {
  const base = live();
  const none = {
    ...base,
    onScreen: null,
    questions: [],
    counts: { open: 0, waiting: 0, answered: 0, hidden: 0 },
  };
  const view = await mount(panel(none));
  await expect(page.getByTestId(IDS.emptyOpen)).toBeVisible();
  for (const [tab, id] of [
    [IDS.tabReview, IDS.emptyReview],
    [IDS.tabAnswered, IDS.emptyAnswered],
    [IDS.tabHidden, IDS.emptyHidden],
  ]) {
    await page.getByTestId(tab).click();
    await expect(page.getByTestId(id)).toBeVisible();
  }
  await page.getByTestId(IDS.tabOpen).click();
  await view.update(panel({ ...none, settings: { ...none.settings, questions: 'closed' } }));
  await expect(page.getByTestId(IDS.closed)).toBeVisible();
  await view.update(panel(qaControlView('notLive')));
  await expect(page.getByTestId(IDS.notLive)).toBeVisible();
  await view.update(panel(live({ settings: { ...base.settings, reactions: false } })));
  await expect(page.getByTestId(IDS.counterOff)).toBeVisible();
  await view.update(
    panel(live({ reactions: { thumbsUp: 0, heart: 0, laugh: 0, wow: 0, clap: 0 } })),
  );
  await expect(page.getByTestId(IDS.counterEmpty)).toBeVisible();
});

test('QaPanel live screenshot', async ({ mount, page }) => {
  await page.setViewportSize({ width: 520, height: 640 });
  await mount(<div className="bg-[#0a0e16] p-4 text-[13px] text-[#e2e8f0]">{panel(live())}</div>);
  await expect(page.getByTestId(IDS.rows)).toBeVisible();
  await expect(page).toHaveScreenshot();
});

test('QaPanel review tab screenshot', async ({ mount, page }) => {
  await page.setViewportSize({ width: 520, height: 420 });
  await mount(
    <div className="bg-[#0a0e16] p-4 text-[13px] text-[#e2e8f0]">
      {panel(live(), { error: 'That question had already changed.' })}
    </div>,
  );
  await page.getByTestId(IDS.tabReview).click();
  await expect(page.getByTestId(qaPanelRowTestId(4))).toBeVisible();
  await expect(page).toHaveScreenshot();
});

test('QaPanel not live screenshot', async ({ mount, page }) => {
  await page.setViewportSize({ width: 520, height: 260 });
  await mount(
    <div className="bg-[#0a0e16] p-4 text-[13px] text-[#e2e8f0]">
      {panel(qaControlView('notLive'))}
    </div>,
  );
  await expect(page.getByTestId(IDS.notLive)).toBeVisible();
  await expect(page).toHaveScreenshot();
});
