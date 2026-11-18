import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { guestView } from '../../test-support/fixtures/guestStates';
import {
  qaControlView,
  questionControl,
  reactionsBurst,
} from '../../test-support/fixtures/qaStates';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import {
  pushQaControl,
  stubQaControlApi,
  type QaControlApiCall,
} from '../../test-support/stubQaControlApi';
import {
  QA_PANEL_TEST_IDS as IDS,
  qaPanelActionTestId,
  qaPanelReactionCountTestId,
  qaPanelRowTestId,
} from '../testIds';
import { QA_ACTIONS, type QaControlView, type ReactionCounts } from '../types';
import { QaControlLive } from './QaControlLive';
import { QaPanel } from './QaPanel';
import { QaControlProbe } from '../../test-support/QaControlProbe';

// The real control provider over the stub hub and the stub Q&A routes, read through the panel. The live sample: seq 212,
// open 1 (14 votes), 2 (on screen) and 6, waiting 4, answered 3, hidden 5; reactions 41/12/7/3/28.

const DECK = 'deck-sw330';
const OTHER_DECK = 'deck-sw330-other';

function presenter(slide: number | null, deckId = DECK, number = 1) {
  return (
    <QaControlLive deckId={deckId} slide={slide}>
      <QaPanel deckId={deckId} />
      <QaControlProbe deckId={deckId} number={number} />
    </QaControlLive>
  );
}

async function stubs(page: Page, options: Parameters<typeof stubQaControlApi>[2] = {}) {
  const hub = await stubDeckHub(page, { audience: { state: guestView('live').state } });
  const api = await stubQaControlApi(page, DECK, options);
  return { hub, api };
}

const reads = (calls: QaControlApiCall[]) => calls.filter((c) => c.call === 'read');
const writes = (calls: QaControlApiCall[]) => calls.filter((c) => c.call !== 'read');
const tab = (page: Page, id: string) => page.getByTestId(id);
const thumbs = (page: Page) => page.getByTestId(qaPanelReactionCountTestId('thumbsUp'));

// Lets the page run its pending work, so a call it should not make has had its chance.
const settle = (page: Page) => page.evaluate(() => new Promise((done) => setTimeout(done, 300)));

const withReactions = (view: QaControlView, reactions: Partial<ReactionCounts>, seq: number) => ({
  ...view,
  seq,
  reactions: { ...view.reactions, ...reactions },
});

test('QaControlLive_WhenMounted_ShouldWatchTheAudienceThenReadTheQuestions', async ({
  mount,
  page,
}) => {
  const { hub, api } = await stubs(page);
  await mount(presenter(5));

  await expect(tab(page, IDS.tabOpen)).toContainText('Open 3');
  await expect(tab(page, IDS.tabReview)).toContainText('To review 1');
  expect(hub.watches).toEqual([DECK]);
  expect(api.calls).toEqual([{ call: 'read' }]);
  // WatchAudience only: no presentation join and no deck join of its own.
  expect(hub.presentationJoins).toEqual([]);
  expect(hub.joined).toEqual([]);
});

test('QaControlLive_WhenQuestionControlArrives_ShouldFollowItAndDropALowerSeq', async ({
  mount,
  page,
}) => {
  const { hub } = await stubs(page);
  await mount(presenter(5));
  await expect(thumbs(page)).toContainText('41');
  const base = qaControlView('live');

  pushQaControl(hub, withReactions(base, { thumbsUp: 50 }, 1000));
  await expect(thumbs(page)).toContainText('50');

  pushQaControl(hub, withReactions(base, { thumbsUp: 60 }, 999));
  pushQaControl(hub, withReactions(base, { thumbsUp: 61 }, 1000));
  await settle(page);
  await expect(thumbs(page)).toContainText('50');

  pushQaControl(hub, withReactions(base, { thumbsUp: 70 }, 1001));
  await expect(thumbs(page)).toContainText('70');
});

test('QaControlLive_WhenQuestionControlArrivesWhileTheReadIsHeld_ShouldKeepItAndDropTheOlderRead', async ({
  mount,
  page,
}) => {
  const { hub, api } = await stubs(page);
  const held = api.hold('read');
  await mount(presenter(5));
  await expect.poll(() => reads(api.calls).length).toBe(1);

  pushQaControl(hub, withReactions(qaControlView('live'), { thumbsUp: 90 }, 5000));
  await expect(thumbs(page)).toContainText('90');

  held.release();
  await settle(page);
  await expect(thumbs(page)).toContainText('90');
});

test('QaControlLive_WhenTheSlideChanges_ShouldReadAgainOncePerChange', async ({ mount, page }) => {
  const { api } = await stubs(page);
  const view = await mount(presenter(5));
  await expect(tab(page, IDS.tabOpen)).toBeVisible();

  await view.update(presenter(6));
  await expect.poll(() => reads(api.calls).length).toBe(2);
  await view.update(presenter(6));
  await settle(page);
  expect(reads(api.calls)).toHaveLength(2);
});

test('QaControlLive_WhenTheDeckGoesLive_ShouldReadAgainAndShowTheNewRound', async ({
  mount,
  page,
}) => {
  const { hub, api } = await stubs(page, { control: qaControlView('notLive') });
  await mount(presenter(5));
  await expect(page.getByTestId(IDS.notLive)).toBeVisible();

  api.setLive(true, 5);
  hub.push({ type: 'liveChanged', deckId: DECK });
  await expect(page.getByTestId(IDS.notLive)).toHaveCount(0);
  await expect(tab(page, IDS.tabOpen)).toContainText('Open 0');
  expect(reads(api.calls)).toHaveLength(2);

  // Another deck's event changes nothing.
  hub.push({ type: 'liveChanged', deckId: OTHER_DECK });
  await settle(page);
  expect(reads(api.calls)).toHaveLength(2);
});

test('QaControlLive_WhenTheDeckIdChanges_ShouldWatchAndReadTheNewDeckAndShowNothingOfTheOld', async ({
  mount,
  page,
}) => {
  const { hub } = await stubs(page);
  const other = await stubQaControlApi(page, OTHER_DECK, { control: qaControlView('notLive') });
  const view = await mount(presenter(5));
  await expect(tab(page, IDS.tabOpen)).toBeVisible();

  await view.update(presenter(5, OTHER_DECK));
  await expect(page.getByTestId(IDS.notLive)).toBeVisible();
  expect(hub.watches).toEqual([DECK, OTHER_DECK]);
  expect(other.calls).toEqual([{ call: 'read' }]);
});

test('QaControlLive_WhenTheConnectionComesBack_ShouldWatchAndReadAgain', async ({
  mount,
  page,
}) => {
  const { hub, api } = await stubs(page);
  await mount(presenter(5));
  await expect(thumbs(page)).toContainText('41');

  // While offline the room reacted; its events were lost.
  api.setControl(withReactions(qaControlView('live'), { thumbsUp: 77 }, 3000));
  hub.drop();

  await expect(thumbs(page)).toContainText('77');
  expect(hub.watches).toEqual([DECK, DECK]);
  expect(reads(api.calls)).toHaveLength(2);
});

test('QaControlLive_WhenTheWatchFails_ShouldStillReadSayLiveUpdatesArentArrivingAndRetry', async ({
  mount,
  page,
}) => {
  const { hub, api } = await stubs(page);
  hub.fail('WatchAudience', 'Server timeout.');
  await mount(presenter(5));

  await expect(tab(page, IDS.tabOpen)).toContainText('Open 3');
  await expect(page.getByTestId(IDS.error)).toContainText("Live question updates aren't reaching");
  expect(reads(api.calls)).toHaveLength(1);

  await expect.poll(() => hub.watches.length, { timeout: 5000 }).toBe(2);
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
  await expect.poll(() => reads(api.calls).length).toBe(2);
});

test('QaControlLive_WhenEachActionIsCalled_ShouldSendItsRouteOnceWithTheQuestionNumber', async ({
  mount,
  page,
}) => {
  const { api } = await stubs(page);
  await mount(presenter(5));
  await expect(tab(page, IDS.tabOpen)).toBeVisible();

  for (const action of QA_ACTIONS) await page.getByTestId(`probe-qa-${action}`).click();
  await expect.poll(() => writes(api.calls).length).toBeGreaterThanOrEqual(QA_ACTIONS.length);
  expect(writes(api.calls).map((c) => c.call)).toEqual([...QA_ACTIONS]);
  expect(writes(api.calls).every((c) => c.number === 1)).toBe(true);
});

test('QaControlLive_WhenAnActionIsRefusedAsWrongState_ShouldShowASentenceNotTheCodeAndReadAgain', async ({
  mount,
  page,
}) => {
  const { api } = await stubs(page);
  await mount(presenter(5));
  await expect(tab(page, IDS.tabOpen)).toBeVisible();

  // Question 1 is open, so approve is refused.
  await page.getByTestId('probe-qa-approve').click();
  await expect(page.getByTestId(IDS.error)).toHaveText(
    'That question had already changed. The buttons now show what you can do.',
  );
  await expect(page.getByTestId(IDS.error)).not.toContainText('wrong_state');
  await expect.poll(() => reads(api.calls).length).toBe(2);
});

test('QaControlLive_WhenTheDeckIsNotLiveAndAnActionIsCalled_ShouldSayToGoLive', async ({
  mount,
  page,
}) => {
  const { api } = await stubs(page);
  await mount(presenter(5));
  await expect(tab(page, IDS.tabOpen)).toBeVisible();

  api.setLive(false);
  await page.getByTestId('probe-qa-hide').click();
  await expect(page.getByTestId(IDS.error)).toHaveText(
    "The deck isn't live, so its questions can't be moderated. Go live first.",
  );
  await expect(page.getByTestId(IDS.error)).not.toContainText('not_live');
  // The re-read shows the deck as not live.
  await expect(page.getByTestId(IDS.notLive)).toBeVisible();
});

test('QaControlLive_WhenAQuestionIsGone_ShouldSayItIsntThereAndReadAgain', async ({
  mount,
  page,
}) => {
  const { api } = await stubs(page);
  const view = await mount(presenter(5));
  await expect(tab(page, IDS.tabOpen)).toBeVisible();

  await view.update(presenter(5, DECK, 99));
  await page.getByTestId('probe-qa-approve').click();
  await expect(page.getByTestId(IDS.error)).toHaveText(
    "Couldn't approve the question. It isn't there any more. The list now shows what is.",
  );
  await expect.poll(() => reads(api.calls).length).toBe(2);
});

test('QaControlLive_WhenAnActionFailsOnTheNetwork_ShouldSayWhatFailedAndReadAgain', async ({
  mount,
  page,
}) => {
  const { api } = await stubs(page, { routes: { hide: 'network-down' } });
  await mount(presenter(5));
  await expect(tab(page, IDS.tabOpen)).toBeVisible();

  await page.getByTestId('probe-qa-hide').click();
  await expect(page.getByTestId(IDS.error)).toHaveText(
    "Couldn't hide the question. Check your connection and try again.",
  );
  await expect.poll(() => reads(api.calls).length).toBe(2);
  // A later good read doesn't wipe the message of the failed call.
  await settle(page);
  await expect(page.getByTestId(IDS.error)).toBeVisible();
});

test('QaControlLive_WhenAFollowingCallStarts_ShouldClearTheLastMessage', async ({
  mount,
  page,
}) => {
  await stubs(page);
  await mount(presenter(5));
  await expect(tab(page, IDS.tabOpen)).toBeVisible();

  await page.getByTestId('probe-qa-approve').click();
  await expect(page.getByTestId(IDS.error)).toBeVisible();
  await page.getByTestId('probe-qa-show').click();
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
});

test('QaControlLive_WhenReactionsArriveBetweenViews_ShouldAddThemToTheViewAndNeverCountThemTwice', async ({
  mount,
  page,
}) => {
  const { hub } = await stubs(page);
  await mount(presenter(5));
  await expect(thumbs(page)).toContainText('41');

  const batch = { thumbsUp: 2, heart: 0, laugh: 0, wow: 0, clap: 0 };
  hub.pushAudienceEvent(reactionsBurst({ counts: batch }));
  await expect(thumbs(page)).toContainText('43');
  hub.pushAudienceEvent(reactionsBurst({ counts: batch }));
  await expect(thumbs(page)).toContainText('45');

  // A newer view already holds both batches: it replaces the sum, it doesn't add to it.
  pushQaControl(hub, withReactions(qaControlView('live'), { thumbsUp: 45 }, 2000));
  await settle(page);
  await expect(thumbs(page)).toContainText('45');
  expect((await thumbs(page).textContent())!.replace(/\D/g, '')).toBe('45');

  hub.pushAudienceEvent(reactionsBurst({ counts: batch }));
  await expect(thumbs(page)).toContainText('47');

  // A view that is not newer is dropped whole, so its totals don't replace the held ones.
  pushQaControl(hub, withReactions(qaControlView('live'), { thumbsUp: 10 }, 2000));
  await settle(page);
  await expect(thumbs(page)).toContainText('47');
});

test('QaControlLive_WhenAnActionMovesTheQuestionAway_ShouldKeepFocusInThePanel', async ({
  mount,
  page,
}) => {
  await stubs(page);
  await mount(presenter(5));
  await expect(page.getByTestId(qaPanelRowTestId(1))).toBeVisible();

  // Show on screen: the pressed button is replaced by "Take off screen"; focus lands on the row's first button.
  await page.getByTestId(qaPanelActionTestId('show', 1)).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId(qaPanelActionTestId('unshow', 1))).toBeVisible();
  await expect(page.getByTestId(qaPanelActionTestId('unshow', 1))).toBeFocused();

  // Answered: the question leaves the Open tab; focus goes to the row now in its place, not to the page.
  await page.getByTestId(qaPanelActionTestId('answer', 1)).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId(qaPanelRowTestId(1))).toHaveCount(0);
  await expect(page.getByTestId(qaPanelRowTestId(2)).locator('button').first()).toBeFocused();

  // Nothing left to take focus: the panel itself.
  await tab(page, IDS.tabReview).click();
  await page.getByTestId(qaPanelActionTestId('approve', 4)).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId(IDS.emptyReview)).toBeVisible();
  await expect(page.getByTestId(IDS.panel)).toBeFocused();
});

test('QaControlLive_WhenQuestionsAreOff_ShouldShowNoRowsAndTheViewIsNeverNull', async ({
  mount,
  page,
}) => {
  const { hub } = await stubs(page);
  await mount(presenter(5));
  await expect(page.getByTestId(qaPanelRowTestId(1))).toBeVisible();

  const base = qaControlView('live');
  hub.pushAudienceEvent(
    questionControl({
      ...base,
      seq: 4000,
      round: null,
      questions: [],
      onScreen: null,
      counts: { open: 0, waiting: 0, answered: 0, hidden: 0 },
      settings: { ...base.settings, questions: 'off' },
    }),
  );
  await expect(page.getByTestId(qaPanelRowTestId(1))).toHaveCount(0);
  await expect(tab(page, IDS.tabOpen)).toHaveCount(0);
  await expect(page.getByTestId(IDS.counter)).toBeVisible();
});

test('QaControlLive_WhenAButtonIsPressedTwiceBeforeTheAnswer_ShouldSendOneCall', async ({
  mount,
  page,
}) => {
  const { api } = await stubs(page);
  await mount(presenter(5));
  const show = page.getByTestId(qaPanelActionTestId('show', 1));
  await expect(show).toBeVisible();

  const held = api.hold('show');
  await show.click();
  await expect.poll(() => writes(api.calls).length).toBe(1);
  await show.click({ force: true });
  await settle(page);
  held.release();
  await expect(page.getByTestId(qaPanelActionTestId('unshow', 1))).toBeVisible();
  await settle(page);
  expect(writes(api.calls)).toEqual([{ call: 'show', number: 1 }]);
});

const REACTION_KEYS = ['thumbsUp', 'heart', 'laugh', 'wow', 'clap'] as const;
const EMOJI = { thumbsUp: '👍', heart: '❤️', laugh: '😂', wow: '😮', clap: '👏' } as const;

// Every counter reads "{emoji} {total}" for the five emoji, in order.
async function expectTotals(page: Page, totals: ReactionCounts) {
  for (const key of REACTION_KEYS)
    await expect(page.getByTestId(qaPanelReactionCountTestId(key))).toHaveText(
      `${EMOJI[key]} ${totals[key]}`,
    );
}

test('QaControlLive_WhenReactionsOfEveryEmojiArrive_ShouldFoldEachOnceAndReplaceThemWithANewerView', async ({
  mount,
  page,
}) => {
  const { hub } = await stubs(page);
  await mount(presenter(5));
  // The live sample: 41 / 12 / 7 / 3 / 28.
  await expectTotals(page, { thumbsUp: 41, heart: 12, laugh: 7, wow: 3, clap: 28 });

  // Different counts per emoji, so a swapped or dropped key shows.
  const batch = { thumbsUp: 1, heart: 2, laugh: 3, wow: 4, clap: 5 };
  hub.pushAudienceEvent(reactionsBurst({ counts: batch }));
  await expectTotals(page, { thumbsUp: 42, heart: 14, laugh: 10, wow: 7, clap: 33 });
  hub.pushAudienceEvent(reactionsBurst({ counts: batch }));
  await expectTotals(page, { thumbsUp: 43, heart: 16, laugh: 13, wow: 11, clap: 38 });

  // A newer view's totals already hold both batches: they replace the sum, every emoji.
  const view = { ...qaControlView('live'), seq: 2000 };
  const totals = { thumbsUp: 100, heart: 101, laugh: 102, wow: 103, clap: 104 };
  pushQaControl(hub, { ...view, reactions: totals });
  await expectTotals(page, totals);
  await settle(page);
  await expectTotals(page, totals);

  hub.pushAudienceEvent(reactionsBurst({ counts: batch }));
  await expectTotals(page, { thumbsUp: 101, heart: 103, laugh: 105, wow: 107, clap: 109 });
});

test('QaControlLive_WhenAnActionIsRefusedOrFailsInOtherWays_ShouldSayWhatToDoNotTheCode', async ({
  mount,
  page,
}) => {
  await stubs(page, {
    routes: {
      hide: { status: 403, json: { error: 'forbidden' } },
      approve: { status: 409, json: { error: 'something_else' } },
      show: { status: 500, json: {} },
    },
  });
  await mount(presenter(5));
  await expect(tab(page, IDS.tabOpen)).toBeVisible();
  const error = page.getByTestId(IDS.error);

  await page.getByTestId('probe-qa-hide').click();
  await expect(error).toHaveText(
    "Couldn't hide the question. Only people who can present this deck can moderate its questions.",
  );
  await page.getByTestId('probe-qa-approve').click();
  await expect(error).toHaveText(
    "Couldn't approve the question. The questions had changed. The list now shows them.",
  );
  await page.getByTestId('probe-qa-show').click();
  await expect(error).toHaveText("Couldn't show the question. Try again in a moment.");
});

test('QaControlLive_WhenAnActionSucceedsAfterTheDeckChanged_ShouldNotShowTheOldDecksView', async ({
  mount,
  page,
}) => {
  const { api } = await stubs(page);
  api.setControl({ ...qaControlView('live'), seq: 9000 });
  await stubQaControlApi(page, OTHER_DECK, { control: qaControlView('notLive') });
  const view = await mount(presenter(5));
  await expect(tab(page, IDS.tabOpen)).toBeVisible();

  const held = api.hold('hide');
  await page.getByTestId('probe-qa-hide').click();
  await expect.poll(() => writes(api.calls).length).toBe(1);
  await view.update(presenter(5, OTHER_DECK));
  await expect(page.getByTestId(IDS.notLive)).toBeVisible();

  held.release();
  await settle(page);
  // The old deck's answer (seq 9001) is not the new deck's view.
  await expect(page.getByTestId(IDS.notLive)).toBeVisible();
  await expect(tab(page, IDS.tabOpen)).toHaveCount(0);
});

test('QaControlLive_WhenAnActionFailsAfterTheDeckChanged_ShouldShowNoMessageAndReadNothingOfTheOldDeck', async ({
  mount,
  page,
}) => {
  const { api } = await stubs(page, { routes: { hide: { status: 500, json: {} } } });
  const other = await stubQaControlApi(page, OTHER_DECK, { control: qaControlView('notLive') });
  const view = await mount(presenter(5));
  await expect(tab(page, IDS.tabOpen)).toBeVisible();

  const held = api.hold('hide');
  await page.getByTestId('probe-qa-hide').click();
  await expect.poll(() => writes(api.calls).length).toBe(1);
  await view.update(presenter(5, OTHER_DECK));
  await expect(page.getByTestId(IDS.notLive)).toBeVisible();

  held.release();
  await settle(page);
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
  // No re-read of the old deck after its failure; the new deck was read once.
  expect(reads(api.calls)).toHaveLength(1);
  expect(other.calls).toEqual([{ call: 'read' }]);
});

test('QaControlLive_WhenTheDeckChanges_ShouldStopListeningForTheOldDecksEvents', async ({
  mount,
  page,
}) => {
  const { hub } = await stubs(page);
  const other = await stubQaControlApi(page, OTHER_DECK, { control: qaControlView('notLive') });
  const view = await mount(presenter(5));
  await expect(tab(page, IDS.tabOpen)).toBeVisible();
  await view.update(presenter(5, OTHER_DECK));
  await expect.poll(() => reads(other.calls).length).toBe(1);

  // The old deck's event must not make the new deck read again.
  hub.push({ type: 'liveChanged', deckId: DECK });
  await settle(page);
  expect(reads(other.calls)).toHaveLength(1);

  hub.push({ type: 'liveChanged', deckId: OTHER_DECK });
  await expect.poll(() => reads(other.calls).length).toBe(2);
});

test('QaControlLive_WhenTheReadFailsThenWorks_ShouldClearTheReadMessageOnly', async ({
  mount,
  page,
}) => {
  const routes: NonNullable<Parameters<typeof stubQaControlApi>[2]>['routes'] = {
    read: 'network-down',
  };
  await stubs(page, { routes });
  const view = await mount(presenter(5));
  await expect(page.getByTestId(IDS.error)).toContainText("Couldn't load the questions");

  delete routes.read;
  await view.update(presenter(6));
  await expect(tab(page, IDS.tabOpen)).toContainText('Open 3');
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
});

test('QaControlLive_WhenTheWatchFailsBeforeAnyViewIsRead_ShouldNotSayLiveUpdatesAreMissingYet', async ({
  mount,
  page,
}) => {
  const { hub, api } = await stubs(page);
  hub.fail('WatchAudience', 'Server timeout.');
  const held = api.hold('read');
  await mount(presenter(5));
  await expect.poll(() => reads(api.calls).length).toBe(1);
  await settle(page);
  // Nothing to be out of date yet.
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);

  held.release();
  await expect(page.getByTestId(IDS.error)).toContainText("Live question updates aren't reaching");
});

test('QaControlLive_WhenAnActionIsCalledFromOutsideThePanel_ShouldNotMoveFocusIntoIt', async ({
  mount,
  page,
}) => {
  await stubs(page);
  await mount(presenter(5));
  await expect(page.getByTestId(qaPanelRowTestId(1))).toBeVisible();

  const probe = page.getByTestId('probe-qa-answer');
  await probe.focus();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId(qaPanelRowTestId(1))).toHaveCount(0);
  await expect(probe).toBeFocused();
});
