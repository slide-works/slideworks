import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { pollControlView } from '../../test-support/fixtures/pollStates';
import type { PollControlView } from '../types';
import {
  POLL_PANEL_TEST_IDS as IDS,
  pollPanelBarTestId,
  pollPanelCorrectTestId,
  pollPanelCountTestId,
  pollPanelHistoryRoundTestId,
  pollPanelRowTestId,
} from '../testIds';
import { PollControlFixture } from './PollControlFixture';
import { PollPanel } from './PollPanel';
import type { PollAction } from './usePollControl';
import { settle } from '../../test-support/gate';

// The poll panel under the frozen fixture provider: every phase of V8, the controls each phase allows, earlier rounds,
// a slide with no poll, a refused call's message, and the P, R and A keys. The real provider has its own spec
// (PollControlLive.spec.tsx); the panel in SW-4's presenter window has PresenterPollKeys.spec.tsx.

const DECK = 'deck-1';
const QUESTION = 'How big was your last merge request?';

// The open sample: round 2, results after close and hidden from the room, round 1 kept.
const hidden = (overrides: Partial<PollControlView> = {}) => pollControlView('open', overrides);
const live = (overrides: Partial<PollControlView> = {}) =>
  pollControlView('open', {
    settings: { ...pollControlView('open').settings, results: 'live' },
    roomSeesCounts: true,
    ...overrides,
  });

function panel(
  control: PollControlView | null,
  {
    error = null,
    onAction,
    overlayOpen = false,
  }: { error?: string | null; onAction?: (a: PollAction) => void; overlayOpen?: boolean } = {},
) {
  return (
    <PollControlFixture deckId={DECK} control={control} error={error} onAction={onAction}>
      <PollPanel deckId={DECK} overlayOpen={overlayOpen} />
      <input data-testid="typing" aria-label="A text field" />
    </PollControlFixture>
  );
}

const button = (page: Page, id: string) => page.getByTestId(IDS.panel).getByTestId(id);
const ALL_BUTTONS = [IDS.open, IDS.close, IDS.reveal, IDS.showAnswer, IDS.newRound];

async function expectButtons(page: Page, shown: string[]) {
  for (const id of ALL_BUTTONS)
    await expect(button(page, id)).toHaveCount(shown.includes(id) ? 1 : 0);
}

// Lets the page run its pending work, so a call it should not make has had its chance.
test('PollPanel_WhenDraft_ShouldOfferOpenOnlyAndSayTheRoundIsNotOpenYet', async ({
  mount,
  page,
}) => {
  await mount(panel(pollControlView('draft')));

  await expect(page.getByTestId(IDS.state)).toHaveText('Draft');
  await expect(page.getByTestId(IDS.round)).toHaveText('round 1');
  await expect(page.getByTestId(IDS.settings)).toHaveText('Pick one · Live');
  await expect(page.getByTestId(IDS.answered)).toHaveText('0 answers');
  await expect(page.getByTestId(IDS.rate)).toHaveText('0% of 37 joined');
  await expect(page.getByTestId(IDS.room)).toHaveText(
    'Round 1 is not open yet. The room sees it once you open it.',
  );
  await expectButtons(page, [IDS.open]);
  await expect(button(page, IDS.open)).toHaveAccessibleName(`Open poll: ${QUESTION}`);
  await expect(page.getByTestId(IDS.history)).toHaveCount(0);
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
  await expect(page.getByTestId(IDS.keys)).toHaveText(
    'P open or close the poll · R reveal · A show answer',
  );
});

test('PollPanel_WhenOpenAndLive_ShouldShowCountsSayTheRoomSeesThemAndOfferCloseOnly', async ({
  mount,
  page,
}) => {
  await mount(panel(live()));

  await expect(page.getByTestId(IDS.state)).toHaveText('Open');
  await expect(page.getByTestId(IDS.settings)).toHaveText('Pick one · Live');
  await expect(page.getByTestId(IDS.answered)).toHaveText('21 answers');
  await expect(page.getByTestId(IDS.rate)).toHaveText('51% of 41 joined');
  await expect(page.getByTestId(IDS.room)).toHaveText('The room sees these results.');
  const counts = [5, 9, 4, 3];
  for (const [position, count] of counts.entries()) {
    await expect(page.getByTestId(pollPanelCountTestId(position))).toHaveText(String(count));
    await expect(page.getByTestId(pollPanelRowTestId(position))).toContainText(
      pollControlView('open').options[position],
    );
  }
  // 9 of 21 answered picked option 2: a bar of 43%.
  await expect(page.getByTestId(pollPanelBarTestId(1))).toHaveAttribute('style', 'width: 43%;');
  await expectButtons(page, [IDS.close]);
  await expect(button(page, IDS.close)).toHaveAccessibleName(`Close poll: ${QUESTION}`);
});

test('PollPanel_WhenOpenAndHiddenFromTheRoom_ShouldStillShowTheCountsAndOfferReveal', async ({
  mount,
  page,
}) => {
  const view = await mount(panel(hidden()));

  await expect(page.getByTestId(IDS.settings)).toHaveText('Pick one · After close');
  await expect(page.getByTestId(IDS.room)).toHaveText(
    'Hidden from the room until the poll closes.',
  );
  // The presenter always sees the counts, whatever the room sees.
  await expect(page.getByTestId(pollPanelCountTestId(1))).toHaveText('9');
  await expectButtons(page, [IDS.close, IDS.reveal]);
  await expect(button(page, IDS.reveal)).toHaveAccessibleName(`Reveal results: ${QUESTION}`);

  await view.update(
    panel(hidden({ settings: { ...pollControlView('open').settings, results: 'afterAnswer' } })),
  );
  await expect(page.getByTestId(IDS.room)).toHaveText(
    'Hidden from the room. Each guest sees them after answering.',
  );

  await view.update(panel(hidden({ revealed: true, roomSeesCounts: true })));
  await expect(page.getByTestId(IDS.room)).toHaveText('Revealed: the room sees these results.');
  await expectButtons(page, [IDS.close]);
});

test('PollPanel_WhenClosedWithACorrectAnswer_ShouldOfferShowAnswerThenMarkTheCorrectRow', async ({
  mount,
  page,
}) => {
  const view = await mount(panel(pollControlView('closed', { answerShown: false })));

  await expect(page.getByTestId(IDS.state)).toHaveText('Closed');
  await expect(page.getByTestId(IDS.settings)).toHaveText('Pick up to 2 · After answering');
  await expect(page.getByTestId(IDS.room)).toHaveText('The room sees the final result.');
  await expectButtons(page, [IDS.showAnswer, IDS.newRound]);
  await expect(button(page, IDS.showAnswer)).toHaveAccessibleName(`Show answer: ${QUESTION}`);
  await expect(button(page, IDS.newRound)).toHaveAccessibleName(`New round: ${QUESTION}`);
  await expect(page.getByTestId(pollPanelCorrectTestId(1))).toHaveCount(0);
  // Pick several: a bar is the share of people who answered that picked the option (25 of 38).
  await expect(page.getByTestId(pollPanelBarTestId(1))).toHaveAttribute('style', 'width: 66%;');
  // The current round is in rounds once closed; it is not listed as an earlier one.
  await expect(page.getByTestId(IDS.history)).toHaveCount(0);

  await view.update(panel(pollControlView('closed')));
  await expect(page.getByTestId(pollPanelCorrectTestId(1))).toHaveText('✓ Correct');
  for (const position of [0, 2, 3])
    await expect(page.getByTestId(pollPanelCorrectTestId(position))).toHaveCount(0);
  await expectButtons(page, [IDS.newRound]);
});

test('PollPanel_WhenPickSeveralHasNoMax_ShouldSayPickSeveral', async ({ mount, page }) => {
  const closed = pollControlView('closed');
  await mount(panel({ ...closed, settings: { ...closed.settings, max: null } }));

  await expect(page.getByTestId(IDS.settings)).toHaveText('Pick several · After answering');
});

test('PollPanel_WhenClosedWithNoCorrectAnswer_ShouldOfferNewRoundOnly', async ({ mount, page }) => {
  const closed = pollControlView('closed');
  await mount(
    panel({ ...closed, answerShown: false, settings: { ...closed.settings, correct: null } }),
  );

  await expectButtons(page, [IDS.newRound]);
});

test('PollPanel_WhenASecondRoundRuns_ShouldListTheFirstAsKept', async ({ mount, page }) => {
  const view = await mount(panel(hidden()));

  await expect(page.getByTestId(IDS.round)).toHaveText('round 2');
  await expect(page.getByTestId(pollPanelHistoryRoundTestId(1))).toHaveText(
    'Round 1: 38 answers, 93% of 41 joined, kept',
  );

  // A draft left by New round on a manual poll: not open yet, with its number, earlier rounds listed.
  await view.update(
    panel(
      pollControlView('draft', {
        number: 3,
        rounds: [
          { number: 1, answered: 38, joined: 41 },
          { number: 2, answered: 1, joined: 41 },
        ],
      }),
    ),
  );
  await expect(page.getByTestId(IDS.state)).toHaveText('Draft');
  await expect(page.getByTestId(IDS.round)).toHaveText('round 3');
  await expect(page.getByTestId(IDS.room)).toHaveText(
    'Round 3 is not open yet. The room sees it once you open it.',
  );
  await expect(page.getByTestId(pollPanelHistoryRoundTestId(1))).toHaveText(
    'Round 1: 38 answers, 93% of 41 joined, kept',
  );
  await expect(page.getByTestId(pollPanelHistoryRoundTestId(2))).toHaveText(
    'Round 2: 1 answer, 2% of 41 joined, kept',
  );
  await expectButtons(page, [IDS.open]);
});

test('PollPanel_WhenMoreHaveAnsweredThanAreJoinedNow_ShouldCapTheRateAt100AndKeepTheNumbers', async ({
  mount,
  page,
}) => {
  const view = await mount(
    panel(
      live({
        answered: 45,
        joined: 40,
        rounds: [
          { number: 1, answered: 50, joined: 41 },
          { number: 3, answered: 0, joined: 0 },
        ],
        number: 4,
      }),
    ),
  );

  await expect(page.getByTestId(IDS.answered)).toHaveText('45 answers');
  await expect(page.getByTestId(IDS.rate)).toHaveText('100% of 40 joined');
  await expect(page.getByTestId(pollPanelHistoryRoundTestId(1))).toHaveText(
    'Round 1: 50 answers, 100% of 41 joined, kept',
  );
  // Nobody joined: the numbers, no percentage.
  await expect(page.getByTestId(pollPanelHistoryRoundTestId(3))).toHaveText(
    'Round 3: 0 answers, 0 joined, kept',
  );

  await view.update(panel(live({ answered: 3, joined: 0 })));
  await expect(page.getByTestId(IDS.rate)).toHaveText('0 joined');
});

test('PollPanel_WhenTheSlideHasNoPoll_ShouldShowNothingOnScreenButSaySoToAssistiveTech', async ({
  mount,
  page,
}) => {
  await mount(panel(null));

  await expect(page.getByTestId(IDS.empty)).toHaveText('No poll on this slide.');
  // Nothing on screen: the panel takes no room in SW-4's window.
  const box = await page.getByTestId(IDS.panel).boundingBox();
  expect(box!.width * box!.height).toBeLessThanOrEqual(1);
  await expect(page.getByTestId(IDS.state)).toHaveCount(0);
  await expect(page.getByTestId(IDS.keys)).toHaveCount(0);
});

test('PollPanel_WhenACallFailed_ShouldShowItsMessage', async ({ mount, page }) => {
  const message = 'A poll on another slide is still open. Close it first, then open this one.';
  const view = await mount(panel(pollControlView('draft'), { error: message }));

  await expect(page.getByTestId(IDS.error)).toHaveText(message);
  await expect(page.getByTestId(IDS.error)).toHaveAttribute('role', 'alert');

  // A read that failed on a slide not yet known to have a poll still shows its message.
  await view.update(panel(null, { error: "Couldn't load this slide's poll." }));
  await expect(page.getByTestId(IDS.error)).toBeVisible();
  await expect(page.getByTestId(IDS.empty)).toBeVisible();
});

test('PollPanel_WhenEachButtonPressed_ShouldCallItsActionOnce', async ({ mount, page }) => {
  const actions: PollAction[] = [];
  const onAction = (a: PollAction) => actions.push(a);
  const view = await mount(panel(pollControlView('draft'), { onAction }));

  await button(page, IDS.open).click();
  await view.update(panel(hidden(), { onAction }));
  await button(page, IDS.reveal).click();
  await button(page, IDS.close).click();
  await view.update(panel(pollControlView('closed', { answerShown: false }), { onAction }));
  await button(page, IDS.showAnswer).click();
  await button(page, IDS.newRound).click();

  await settle(page, 200);
  expect(actions).toEqual(['open', 'reveal', 'close', 'showAnswer', 'newRound']);
});

test('PollPanel_WhenCountsChange_ShouldKeepFocusOnTheButton', async ({ mount, page }) => {
  const view = await mount(panel(live()));
  await button(page, IDS.close).focus();

  await view.update(panel(live({ answered: 22, counts: [5, 10, 4, 3], seq: 132 })));

  await expect(page.getByTestId(pollPanelCountTestId(1))).toHaveText('10');
  await expect(button(page, IDS.close)).toBeFocused();
});

test('PollPanel_WhenPPressed_ShouldOpenADraftAndCloseAnOpenPoll', async ({ mount, page }) => {
  const actions: PollAction[] = [];
  const onAction = (a: PollAction) => actions.push(a);
  const view = await mount(panel(pollControlView('draft'), { onAction }));
  await expect(page.getByTestId(IDS.state)).toHaveText('Draft');

  await page.keyboard.press('p');
  await expect.poll(() => actions).toEqual(['open']);
  await view.update(panel(live(), { onAction }));
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');
  await page.keyboard.press('P');
  await expect.poll(() => actions).toEqual(['open', 'close']);
  // Closed: P has nothing to do.
  await view.update(panel(pollControlView('closed'), { onAction }));
  await expect(page.getByTestId(IDS.state)).toHaveText('Closed');
  await page.keyboard.press('p');

  await settle(page, 200);
  expect(actions).toEqual(['open', 'close']);
});

test('PollPanel_WhenRPressed_ShouldRevealOnlyWhileHiddenFromTheRoom', async ({ mount, page }) => {
  const actions: PollAction[] = [];
  const onAction = (a: PollAction) => actions.push(a);
  const view = await mount(panel(live(), { onAction }));
  await expect(page.getByTestId(IDS.room)).toHaveText('The room sees these results.');

  await page.keyboard.press('r');
  await view.update(panel(hidden(), { onAction }));
  await expect(page.getByTestId(IDS.room)).toHaveText(
    'Hidden from the room until the poll closes.',
  );
  await page.keyboard.press('r');

  await expect.poll(() => actions).toEqual(['reveal']);
  await settle(page, 200);
  expect(actions).toEqual(['reveal']);
});

test('PollPanel_WhenAPressed_ShouldShowTheAnswerOnlyWhenClosedWithOneNotShown', async ({
  mount,
  page,
}) => {
  const actions: PollAction[] = [];
  const onAction = (a: PollAction) => actions.push(a);
  const closed = pollControlView('closed', { answerShown: false });
  const view = await mount(panel(live(), { onAction }));
  const showAnswer = button(page, IDS.showAnswer);
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');

  await page.keyboard.press('a');
  // Closed with no correct answer.
  await view.update(
    panel({ ...closed, settings: { ...closed.settings, correct: null } }, { onAction }),
  );
  await expect(page.getByTestId(IDS.state)).toHaveText('Closed');
  await page.keyboard.press('a');
  // Closed with the answer already shown.
  await view.update(panel(pollControlView('closed'), { onAction }));
  await expect(page.getByTestId(pollPanelCorrectTestId(1))).toBeVisible();
  await page.keyboard.press('a');
  await view.update(panel(closed, { onAction }));
  await expect(showAnswer).toBeVisible();
  await page.keyboard.press('a');

  await expect.poll(() => actions).toEqual(['showAnswer']);
  await settle(page, 200);
  expect(actions).toEqual(['showAnswer']);
});

test('PollPanel_WhenNoPollOrTypingOrAnOverlayIsOpen_ShouldIgnoreTheKeys', async ({
  mount,
  page,
}) => {
  const actions: PollAction[] = [];
  const onAction = (a: PollAction) => actions.push(a);
  const view = await mount(panel(null, { onAction }));
  await expect(page.getByTestId(IDS.empty)).toBeAttached();
  for (const key of ['p', 'r', 'a']) await page.keyboard.press(key);

  await view.update(panel(hidden(), { onAction }));
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');
  await page.getByTestId('typing').focus();
  for (const key of ['p', 'r']) await page.keyboard.press(key);
  await expect(page.getByTestId('typing')).toHaveValue('pr');
  await page.getByTestId('typing').blur();

  await view.update(panel(hidden(), { onAction, overlayOpen: true }));
  await settle(page, 200);
  for (const key of ['p', 'r']) await page.keyboard.press(key);
  // With a modifier held the key belongs to the browser or the system.
  await view.update(panel(hidden(), { onAction }));
  await settle(page, 200);
  await page.keyboard.press('Alt+p');

  await settle(page, 200);
  expect(actions).toEqual([]);

  // The same panel still answers a plain key, so the presses above were really ignored.
  await page.keyboard.press('r');
  await expect.poll(() => actions).toEqual(['reveal']);
});

test('PollPanel_WhenForcedColorsAreActive_ShouldKeepTheBarsTheCorrectMarkAndTheButtonEdges', async ({
  mount,
  page,
}) => {
  await page.emulateMedia({ forcedColors: 'active' });
  const view = await mount(panel(pollControlView('closed')));

  // The fill keeps a system Highlight background of its own (the mode would paint it as the background), inside a
  // bordered track, so the bar stays visible.
  const fill = page.getByTestId(pollPanelBarTestId(1));
  const styles = await fill.evaluate((element) => {
    const own = getComputedStyle(element);
    const track = getComputedStyle(element.parentElement!);
    return {
      adjust: own.forcedColorAdjust,
      fill: own.backgroundColor,
      track: track.backgroundColor,
      trackBorder: track.borderTopWidth,
    };
  });
  expect(styles.adjust).toBe('none');
  expect(styles.fill).not.toBe(styles.track);
  expect(styles.fill).not.toBe('rgba(0, 0, 0, 0)');
  expect(styles.trackBorder).toBe('1px');
  expect((await fill.boundingBox())!.width).toBeGreaterThan(0);

  // The correct row says so in words, in a box.
  const correct = page.getByTestId(pollPanelCorrectTestId(1));
  await expect(correct).toHaveText('✓ Correct');
  await expect(correct).toHaveCSS('border-top-width', '1px');
  // The state pill has an edge.
  await expect(page.getByTestId(IDS.state)).toHaveCSS('border-top-width', '1px');
  await expect(page.getByTestId(IDS.room)).toHaveText('The room sees the final result.');

  // Every button has an edge.
  await expect(button(page, IDS.newRound)).toHaveCSS('border-top-width', '2px');
  await view.update(panel(hidden()));
  await expect(page.getByTestId(IDS.room)).toHaveText(
    'Hidden from the room until the poll closes.',
  );
  for (const id of [IDS.close, IDS.reveal])
    await expect(button(page, id)).toHaveCSS('border-top-width', '2px');
});

test('PollPanel open hidden screenshot', async ({ mount, page }) => {
  await page.setViewportSize({ width: 520, height: 520 });
  await mount(
    <div className="bg-[#0a0e16] p-4 text-[13px] text-[#e2e8f0]">
      <PollControlFixture deckId={DECK} control={hidden()}>
        <PollPanel deckId={DECK} />
      </PollControlFixture>
    </div>,
  );
  await expect(page.getByTestId(IDS.state)).toHaveText('Open');
  await expect(page).toHaveScreenshot();
});
