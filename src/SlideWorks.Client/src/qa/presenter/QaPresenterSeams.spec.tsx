import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AppRoutes } from '../../AppRoutes';
import type { LooksView } from '../../api/types';
import { guestView } from '../../test-support/fixtures/guestStates';
import {
  PALETTES,
  TEMPLATES,
  looksPaletteOf,
  looksTemplateOf,
} from '../../test-support/fixtures/lookViewOf';
import { presentationState, presenterView } from '../../test-support/fixtures/presenting';
import { qaControlView } from '../../test-support/fixtures/qaStates';
import { deckPage, me } from '../../test-support/fixtures/screens';
import { QaControlProbe } from '../../test-support/QaControlProbe';
import { stubApi } from '../../test-support/stubApi';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { stubPollControlApi } from '../../test-support/stubPollControlApi';
import { stubQaControlApi } from '../../test-support/stubQaControlApi';
import {
  QA_PANEL_TEST_IDS,
  QA_SETTINGS_CARD_TEST_IDS,
  QA_SETTINGS_STRIP_TEST_IDS,
  QA_STRIP_TEST_IDS,
} from '../testIds';
import type { QaAction, QaControlView } from '../types';
import {
  QUESTIONS_TOOL_COUNT_TEST_ID,
  QUESTIONS_TOOL_TEST_ID,
} from '../../presentation/presenter/ToolBar';
import { QaControlFixture, type QaControlCall } from './QaControlFixture';
import { QaControlLive } from './QaControlLive';
import { QaPanel } from './QaPanel';
import { QaSettingsCard } from './QaSettingsCard';
import { QaStrip } from './QaStrip';
import { QuestionsToolBar } from './QuestionsToolBar';

// SW-9 wave 0.2's presenter seams in the browser: useQaControl under its fixture, the stubs' root test ids, Q and the
// Questions tool in the presenter window, the strip on the phone remote, the card on the deck page, and the stub of the
// presenter's Q&A routes. Specs assert root test ids, hook values and recorded calls only, never a stub's text.

const view = presenterView();
const deckId = view.id;
const presenterPath = `/api/decks/${deckId}/presenter`;

const paused = (slide: number) =>
  presentationState({ slide, step: 0, timer: { state: 'paused', elapsedMs: 760000 } });

test.beforeEach(async ({ page }) => {
  await page.route(`**${view.look.logo!.url}`, (r) =>
    r.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
});

type Mount = (
  component: React.JSX.Element,
  options?: { hooksConfig: { route: string } },
) => Promise<unknown>;

// SW-4's presenter window or phone remote over the stub hub, the stub poll routes (no poll) and the stub Q&A routes.
async function openWindow(page: Page, mount: Mount, path: string) {
  await stubDeckHub(page, {
    presentation: { state: paused(8) },
    audience: { state: guestView('live').state },
  });
  await stubApi(page, { '/api/me': { json: me }, [presenterPath]: { json: view } });
  await stubPollControlApi(page, deckId, { polls: [] });
  const api = await stubQaControlApi(page, deckId);
  await mount(<AppRoutes />, { hooksConfig: { route: path } });
  return api;
}

async function openPresenter(page: Page, mount: Mount) {
  const api = await openWindow(page, mount, `/decks/${deckId}/present`);
  await expect(page.getByTestId('presenter-now')).toBeVisible();
  await expect(page.getByTestId('presenter-page')).toBeFocused();
  return api;
}

// Sends one presenter route from the page, as qaControlApi does.
type Answer = { status: number; body: QaControlView & { error?: string } };
function send(
  page: Page,
  path: string,
  method: 'GET' | 'POST' | 'PUT' = 'GET',
  body?: unknown,
): Promise<Answer> {
  return page.evaluate(
    async ([url, verb, json]) => {
      const response = await fetch(url, {
        method: verb,
        headers: json === undefined ? {} : { 'Content-Type': 'application/json' },
        body: json === undefined ? undefined : JSON.stringify(json),
      });
      return { status: response.status, body: await response.json() };
    },
    [`/api/decks/${deckId}/qa${path}`, method, body] as const,
  );
}
const act = (page: Page, action: QaAction, number: number) =>
  send(page, `/questions/${number}/${action}`, 'POST');

// The test ids in document order, among those given: where a part sits next to SW-4's and SW-6's parts.
const orderOf = (page: Page, ids: string[]) =>
  page.evaluate(
    (wanted) =>
      [...document.querySelectorAll('[data-testid]')]
        .map((element) => element.getAttribute('data-testid')!)
        .filter((id) => wanted.includes(id)),
    ids,
  );

test('QaControlFixture_WhenItsPartsAct_ShouldGiveTheViewAndRecordEachCall', async ({
  mount,
  page,
}) => {
  const calls: QaControlCall[] = [];
  const control = qaControlView('live');
  await mount(
    <QaControlFixture
      deckId="deck-1"
      control={control}
      error="refused"
      onAction={(call) => calls.push(call)}
    >
      <QaControlProbe deckId="deck-1" number={4} />
    </QaControlFixture>,
  );
  expect(JSON.parse((await page.getByTestId('probe-qa-control').textContent())!)).toEqual(control);
  await expect(page.getByTestId('probe-qa-error')).toHaveText('"refused"');

  const actions: QaAction[] = ['approve', 'hide', 'restore', 'answer', 'reopen', 'show', 'unshow'];
  for (const action of actions) await page.getByTestId(`probe-qa-${action}`).click();
  await page.getByTestId('probe-qa-settings').click();
  await page.getByTestId('probe-qa-slide').click();
  await expect.poll(() => calls.length).toBe(9);
  expect(calls).toEqual([
    ...actions.map((action) => ({ action, number: 4 })),
    { action: 'settings', patch: { review: false } },
    { action: 'slide', slide: 3, patch: { questions: true } },
  ]);
});

test('QaControlFixture_WhenNotRead_ShouldGiveNullAndNoError', async ({ mount, page }) => {
  await mount(
    <QaControlFixture deckId="deck-1" control={null}>
      <QaControlProbe deckId="deck-1" />
    </QaControlFixture>,
  );
  await expect(page.getByTestId('probe-qa-control')).toHaveText('null');
  await expect(page.getByTestId('probe-qa-error')).toHaveText('null');
});

test('QaPresenterParts_WhenUnderTheFixture_ShouldRenderTheirRootTestIds', async ({
  mount,
  page,
}) => {
  await mount(
    <QaControlFixture deckId="deck-1" control={qaControlView('live')}>
      <QaPanel deckId="deck-1" />
      <QaStrip deckId="deck-1" />
      <QaSettingsCard deckId="deck-1" slide={3} />
    </QaControlFixture>,
  );
  await expect(page.getByTestId(QA_PANEL_TEST_IDS.panel)).toHaveCount(1);
  await expect(
    page.getByTestId(QA_PANEL_TEST_IDS.panel).getByTestId(QA_SETTINGS_STRIP_TEST_IDS.strip),
  ).toHaveCount(1);
  await expect(page.getByTestId(QA_STRIP_TEST_IDS.strip)).toHaveCount(1);
  await expect(page.getByTestId(QA_SETTINGS_CARD_TEST_IDS.card)).toHaveCount(1);
});

test('QaControlLive_WhenGivenTheSlide_ShouldFollowIt', async ({ mount, page }) => {
  const shown = await mount(
    <QaControlLive deckId="deck-1" slide={5}>
      <QaControlProbe deckId="deck-1" />
    </QaControlLive>,
  );
  const slide = page.getByTestId('probe-qa-provider-slide');
  await expect(slide).toHaveText('5');
  await shown.update(
    <QaControlLive deckId="deck-1" slide={6}>
      <QaControlProbe deckId="deck-1" />
    </QaControlLive>,
  );
  await expect(slide).toHaveText('6');
  await shown.update(
    <QaControlLive deckId="deck-1" slide={null}>
      <QaControlProbe deckId="deck-1" />
    </QaControlLive>,
  );
  await expect(slide).toHaveText('null');
});

const toolBarProps = {
  deckId: 'deck-1',
  overviewOpen: false,
  notesShown: true,
  skipDeepDives: false,
  black: false,
  phonesConnected: 0,
  onOverview: () => {},
  onNotes: () => {},
  onSkip: () => {},
  onBlack: () => {},
  onPhone: () => {},
  onQuestions: () => {},
};

test('QuestionsToolBar_WhenTheViewHasQuestionsWaitingOrOpen_ShouldShowTheirCountOnTheTool', async ({
  mount,
  page,
}) => {
  // The live sample has 3 open and 1 waiting.
  const shown = await mount(
    <QaControlFixture deckId="deck-1" control={qaControlView('live')}>
      <QuestionsToolBar {...toolBarProps} />
    </QaControlFixture>,
  );
  const count = page.getByTestId(QUESTIONS_TOOL_TEST_ID).getByTestId(QUESTIONS_TOOL_COUNT_TEST_ID);
  await expect(count).toHaveText(/(^|\D)4(\D|$)/);

  const none = qaControlView('live');
  await shown.update(
    <QaControlFixture
      deckId="deck-1"
      control={{ ...none, counts: { ...none.counts, open: 0, waiting: 0 } }}
    >
      <QuestionsToolBar {...toolBarProps} />
    </QaControlFixture>,
  );
  await expect(count).toHaveCount(0);
  await shown.update(
    <QaControlFixture deckId="deck-1" control={null}>
      <QuestionsToolBar {...toolBarProps} />
    </QaControlFixture>,
  );
  await expect(count).toHaveCount(0);
  await expect(page.getByTestId(QUESTIONS_TOOL_TEST_ID)).toHaveCount(1);
});

test('PresenterPage_WhenQPressed_ShouldOpenAndCloseTheQuestionsPanel', async ({ mount, page }) => {
  const api = await openPresenter(page, mount);
  const panel = page.getByTestId(QA_PANEL_TEST_IDS.panel);
  const tool = page.getByTestId(QUESTIONS_TOOL_TEST_ID);
  await expect(panel).toHaveCount(0);
  await expect(tool).toHaveAttribute('aria-pressed', 'false');

  await page.keyboard.press('q');
  await expect(panel).toHaveCount(1);
  await expect(tool).toHaveAttribute('aria-pressed', 'true');
  // In the right column, after Next (and SW-6's poll panel, absent on a slide with no poll) and before the notes.
  expect(
    await orderOf(page, ['presenter-next', QA_PANEL_TEST_IDS.panel, 'presenter-notes']),
  ).toEqual(['presenter-next', QA_PANEL_TEST_IDS.panel, 'presenter-notes']);
  await page.keyboard.press('Q');
  await expect(panel).toHaveCount(0);
  await expect(tool).toHaveAttribute('aria-pressed', 'false');
  // Closing gives focus back to the tool, as the overview and the phone dialog do, never to the page's body.
  await expect(tool).toBeFocused();
  // The stub provider has no view yet, so the tool shows no count.
  await expect(page.getByTestId(QUESTIONS_TOOL_COUNT_TEST_ID)).toHaveCount(0);

  await tool.click();
  await expect(panel).toHaveCount(1);
  await tool.click();
  await expect(panel).toHaveCount(0);
  // Q drives nothing else: no move of the talk and no Q&A call.
  await expect(page.getByTestId('presenter-now')).toBeVisible();
  expect(api.calls.filter((c) => c.call !== 'read')).toEqual([]);
});

test('PresenterPage_WhenQPressedInATextBox_ShouldLeaveThePanelAlone', async ({ mount, page }) => {
  await openPresenter(page, mount);
  await page.evaluate(() => {
    const box = document.createElement('input');
    box.dataset.testid = 'probe-text';
    document.body.append(box);
  });
  const box = page.getByTestId('probe-text');
  await box.focus();
  await page.keyboard.press('q');
  await expect(box).toHaveValue('q');
  await expect(page.getByTestId(QA_PANEL_TEST_IDS.panel)).toHaveCount(0);
  await expect(page.getByTestId(QUESTIONS_TOOL_TEST_ID)).toHaveAttribute('aria-pressed', 'false');
});

test('PresenterPage_WhenQPressedOverTheOverview_ShouldDoNothing', async ({ mount, page }) => {
  await openPresenter(page, mount);
  await page.keyboard.press('o');
  await expect(page.getByTestId('presenter-tool-overview')).toHaveAttribute('aria-pressed', 'true');
  await page.keyboard.press('q');
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('presenter-tool-overview')).toHaveAttribute(
    'aria-pressed',
    'false',
  );
  await expect(page.getByTestId(QA_PANEL_TEST_IDS.panel)).toHaveCount(0);
});

test('SpeakerPage_WhenOpen_ShouldPlaceTheQuestionsStripAboveTheNotes', async ({ mount, page }) => {
  await openWindow(page, mount, `/remote/${deckId}`);
  await expect(page.getByTestId('speaker-status')).toHaveAttribute('data-status', 'live');
  await expect(page.getByTestId('speaker-page').getByTestId(QA_STRIP_TEST_IDS.strip)).toHaveCount(
    1,
  );
  // Under the slide and SW-6's poll strip (absent on a slide with no poll), above the notes.
  expect(await orderOf(page, ['speaker-now', QA_STRIP_TEST_IDS.strip, 'speaker-notes'])).toEqual([
    'speaker-now',
    QA_STRIP_TEST_IDS.strip,
    'speaker-notes',
  ]);
});

// The owner's deck page, or another viewer's, over the stub hub (the page connects through useDeckRealtime).
async function openDeckPage(page: Page, mount: Mount, isOwner: boolean) {
  const looks: LooksView = {
    templates: TEMPLATES.map(looksTemplateOf),
    palettes: PALETTES.map(looksPaletteOf),
  };
  await page.route('**/api/looks', (route) => route.fulfill({ json: looks }));
  await page.route(`**${deckPage.look.logo!.url}`, (route) =>
    route.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
  await stubDeckHub(page);
  await stubApi(page, {
    '/api/me': { json: me },
    [`/api/decks/${deckPage.id}`]: { json: { ...deckPage, isOwner } },
  });
  await mount(<AppRoutes />, { hooksConfig: { route: `/decks/${deckPage.id}` } });
  await expect(page.getByTestId('deck-page')).toBeVisible();
}

test('DeckPage_WhenTheViewerCanPresent_ShouldPlaceTheSettingsCard', async ({ mount, page }) => {
  await openDeckPage(page, mount, true);
  await expect(page.getByTestId(QA_SETTINGS_CARD_TEST_IDS.card)).toHaveCount(1);
});

test('DeckPage_WhenTheViewerCannotPresent_ShouldLeaveTheSettingsCardOut', async ({
  mount,
  page,
}) => {
  await openDeckPage(page, mount, false);
  await expect(page.getByTestId(QA_SETTINGS_CARD_TEST_IDS.card)).toHaveCount(0);
});

test('stubQaControlApi_WhenApproveIsSentForAnOpenQuestion_ShouldRefuseWithWrongStateAndChangeNothing', async ({
  page,
}) => {
  await stubQaControlApi(page, deckId);
  const before = (await send(page, '')).body;
  // Question 1 of the live sample is open.
  expect(await act(page, 'approve', 1)).toEqual({ status: 409, body: { error: 'wrong_state' } });
  const after = (await send(page, '')).body;
  expect(after.seq).toBeGreaterThan(before.seq);
  expect({ ...after, seq: 0 }).toEqual({ ...before, seq: 0 });
});

test('stubQaControlApi_WhenNotLive_ShouldRefuseEveryQuestionRouteWithNotLiveButTakeSettings', async ({
  page,
}) => {
  const api = await stubQaControlApi(page, deckId, { control: qaControlView('notLive') });
  for (const action of ['approve', 'hide', 'show'] as const)
    expect(await act(page, action, 1)).toEqual({ status: 409, body: { error: 'not_live' } });

  const changed = await send(page, '/settings', 'PUT', { review: true });
  expect(changed.status).toBe(200);
  expect(changed.body.settings.review).toBe(true);
  expect(changed.body.live).toBe(false);
  expect(api.calls.map((c) => c.call)).toEqual(['approve', 'hide', 'show', 'settings']);
});

test('stubQaControlApi_WhenModerating_ShouldFollowTheLifecycleAndRaiseSeq', async ({ page }) => {
  await stubQaControlApi(page, deckId);
  const start = (await send(page, '')).body;
  // Question 4 waits for review: approve it, show it (taking 2 off screen), answer it, reopen it.
  const approved = await act(page, 'approve', 4);
  expect(approved.status).toBe(200);
  expect(approved.body.seq).toBeGreaterThan(start.seq);
  expect(approved.body.counts).toEqual({ ...start.counts, open: 4, waiting: 0 });
  const shown = (await act(page, 'show', 4)).body;
  expect(shown.onScreen).toBe(4);
  expect(shown.questions.filter((q) => q.onScreen).map((q) => q.number)).toEqual([4]);
  const answered = (await act(page, 'answer', 4)).body;
  expect(answered.onScreen).toBeNull();
  expect(answered.questions.find((q) => q.number === 4)!.state).toBe('answered');
  expect((await act(page, 'hide', 4)).body).toEqual({ error: 'wrong_state' });
  expect((await act(page, 'reopen', 4)).status).toBe(200);
  // Question 5 is hidden: it can't be shown or answered until restored, nor hidden again.
  expect((await act(page, 'show', 5)).body).toEqual({ error: 'wrong_state' });
  expect((await act(page, 'hide', 5)).body).toEqual({ error: 'wrong_state' });
  // Hiding the question on screen takes it off.
  expect((await act(page, 'show', 1)).body.onScreen).toBe(1);
  const hidden = (await act(page, 'hide', 1)).body;
  expect(hidden.onScreen).toBeNull();
  expect(hidden.questions.find((q) => q.number === 1)!.onScreen).toBe(false);
  // Question 6 is open and not hidden: nothing to reopen or restore.
  expect((await act(page, 'reopen', 6)).body).toEqual({ error: 'wrong_state' });
  expect((await act(page, 'restore', 6)).body).toEqual({ error: 'wrong_state' });
  expect((await act(page, 'restore', 5)).status).toBe(200);
  expect((await act(page, 'unshow', 5)).body).toEqual({ error: 'wrong_state' });
  expect((await act(page, 'approve', 99)).status).toBe(404);
});

test('stubQaControlApi_WhenASlideSwitchIsSet_ShouldListOnlySlidesWithSomethingOff', async ({
  page,
}) => {
  await stubQaControlApi(page, deckId);
  const on = (await send(page, '/slides/3', 'PUT', { questions: true })).body;
  expect(on.slides).toEqual([]);
  const off = (await send(page, '/slides/5', 'PUT', { reactions: false })).body;
  expect(off.slides).toEqual([{ slide: 5, questions: true, reactions: false }]);
  expect(off.here).toEqual({ slide: 5, questions: true, reactions: false });
});
