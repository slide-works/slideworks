import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AppRoutes } from '../../AppRoutes';
import { guestView } from '../../test-support/fixtures/guestStates';
import { pollControlView } from '../../test-support/fixtures/pollStates';
import { presentationState, presenterView } from '../../test-support/fixtures/presenting';
import { me } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { stubDeckHub } from '../../test-support/stubDeckHub';
import { stubPollControlApi } from '../../test-support/stubPollControlApi';
import type { PollControlView } from '../types';
import { POLL_PANEL_TEST_IDS as IDS } from '../testIds';
import { settle } from '../../test-support/gate';

// The poll panel in SW-4's presenter window, over the stub hub and the stub poll routes: where it sits, and the P, R and
// A keys beside SW-4's own. The window starts on slide 8; the poll is on slide 9 unless a test says otherwise.

const view = presenterView();
const deckId = view.id;
const route = { hooksConfig: { route: `/decks/${deckId}/present` } };
const presenterPath = `/api/decks/${deckId}/presenter`;
const START = 8;
const POLL = 9;

const paused = (slide: number) =>
  presentationState({ slide, step: 0, timer: { state: 'paused', elapsedMs: 760000 } });

// A manual poll, results after close, with a correct answer: every key has something to do in some phase.
const manualPoll = (slide: number): PollControlView => {
  const draft = pollControlView('draft', { slide });
  return {
    ...draft,
    roomSeesCounts: false,
    settings: { ...draft.settings, results: 'afterClose', correct: 2 },
  };
};

test.beforeEach(async ({ page }) => {
  await page.route(`**${view.look.logo!.url}`, (r) =>
    r.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
});

async function openPresenter(
  page: Page,
  mount: (component: React.JSX.Element, options: typeof route) => Promise<unknown>,
  { slide = START, polls = [manualPoll(POLL)] }: { slide?: number; polls?: PollControlView[] } = {},
) {
  const hub = await stubDeckHub(page, {
    presentation: { state: paused(slide) },
    audience: { state: guestView('live').state },
  });
  await stubApi(page, { '/api/me': { json: me }, [presenterPath]: { json: view } });
  const api = await stubPollControlApi(page, deckId, { polls });
  await mount(<AppRoutes />, route);
  await expect(page.getByTestId('presenter-now')).toBeVisible();
  await expect(page.getByTestId('presenter-page')).toBeFocused();
  return { hub, api };
}

const actions = (api: Awaited<ReturnType<typeof stubPollControlApi>>) =>
  api.calls.filter((c) => c.call !== 'read').map((c) => c.call);

// Lets the page run its pending work, so a call it should not make has had its chance.
test('PresenterPollPanel_WhenOnAPollSlide_ShouldShowThePanelBetweenNextAndNotes', async ({
  mount,
  page,
}) => {
  const { hub } = await openPresenter(page, mount, { slide: POLL });

  const panel = page.getByTestId(IDS.panel);
  await expect(panel.getByTestId(IDS.state)).toHaveText('Draft');
  const next = (await page.getByTestId('presenter-next').boundingBox())!;
  const box = (await panel.boundingBox())!;
  const notes = (await page.getByTestId('presenter-notes').boundingBox())!;
  expect(box.y).toBeGreaterThan(next.y + next.height - 1);
  expect(notes.y).toBeGreaterThan(box.y + box.height - 1);
  // The window's one presentation join is SW-4's; the panels only watch the audience: one watch each from the poll's
  // provider, the open-response provider, and SW-9's Q&A provider.
  expect(hub.presentationJoins).toHaveLength(1);
  expect(hub.watches).toEqual([deckId, deckId, deckId]);
});

test('PresenterPollPanel_WhenPRAndAPressed_ShouldRunThePollThroughItsRoutes', async ({
  mount,
  page,
}) => {
  const { api } = await openPresenter(page, mount, { slide: POLL });
  const state = page.getByTestId(IDS.state);
  await expect(state).toHaveText('Draft');

  // A has nothing to do before the poll closes, R nothing before it opens.
  await page.keyboard.press('a');
  await page.keyboard.press('r');
  await page.keyboard.press('p');
  await expect(state).toHaveText('Open');
  await page.keyboard.press('r');
  await expect(page.getByTestId(IDS.room)).toHaveText('Revealed: the room sees these results.');
  await page.keyboard.press('p');
  await expect(state).toHaveText('Closed');
  await page.keyboard.press('a');
  await expect(page.getByTestId(IDS.panel).getByTestId(IDS.showAnswer)).toHaveCount(0);
  // Closed with the answer shown: none of the three keys has anything left to do.
  for (const key of ['p', 'r', 'a']) await page.keyboard.press(key);

  await settle(page, 300);
  expect(actions(api)).toEqual(['open', 'reveal', 'close', 'showAnswer']);
  await expect(page.getByTestId(IDS.error)).toHaveCount(0);
});

test('PresenterPollPanel_WhenPROrAIsHeld_ShouldActOnceAndIgnoreTheRepeats', async ({
  mount,
  page,
}) => {
  const { api } = await openPresenter(page, mount, { slide: POLL });
  const state = page.getByTestId(IDS.state);
  await expect(state).toHaveText('Draft');

  // Held P: the first keydown opens; the auto-repeats that follow once Close is in P's place must not close.
  await page.keyboard.down('p');
  await expect(state).toHaveText('Open');
  await page.keyboard.down('p');
  await page.keyboard.down('p');
  await page.keyboard.up('p');
  await settle(page, 300);
  await expect(state).toHaveText('Open');

  // Held R reveals once.
  await page.keyboard.down('r');
  await expect(page.getByTestId(IDS.room)).toHaveText('Revealed: the room sees these results.');
  await page.keyboard.down('r');
  await page.keyboard.up('r');

  // Closed, then held A shows the answer once.
  await page.keyboard.press('p');
  await expect(state).toHaveText('Closed');
  await page.keyboard.down('a');
  await expect(page.getByTestId(IDS.panel).getByTestId(IDS.showAnswer)).toHaveCount(0);
  await page.keyboard.down('a');
  await page.keyboard.up('a');

  await settle(page, 300);
  expect(actions(api)).toEqual(['open', 'reveal', 'close', 'showAnswer']);
});

test('PresenterPollPanel_WhenTheSlideHasNoPoll_ShouldShowNoPanelAndIgnorePRAndA', async ({
  mount,
  page,
}) => {
  const { api, hub } = await openPresenter(page, mount);
  await expect.poll(() => api.calls).toEqual([{ call: 'read', slide: START }]);

  await expect(page.getByTestId(IDS.empty)).toBeAttached();
  await expect(page.getByTestId(IDS.state)).toHaveCount(0);
  for (const key of ['p', 'r', 'a']) await page.keyboard.press(key);

  await settle(page, 300);
  expect(actions(api)).toEqual([]);
  expect(hub.updates).toEqual([]);
});

test('PresenterPollPanel_WhenTypingOrAnOverlayIsOpen_ShouldIgnorePAndKeepOAndNWorking', async ({
  mount,
  page,
}) => {
  const { api } = await openPresenter(page, mount, { slide: POLL });
  await expect(page.getByTestId(IDS.state)).toHaveText('Draft');

  // A text field somewhere in the window: its keys are its own.
  await page.evaluate(() => {
    const field = document.createElement('input');
    field.setAttribute('data-testid', 'typing');
    field.setAttribute('aria-label', 'A text field');
    document.body.append(field);
  });
  await page.getByTestId('typing').focus();
  await page.keyboard.type('par');
  await expect(page.getByTestId('typing')).toHaveValue('par');
  await page.getByTestId('presenter-page').focus();

  // The overview is open on top: P does nothing, O still closes it.
  await page.keyboard.press('o');
  await expect(page.getByTestId('overview')).toBeVisible();
  await page.keyboard.press('p');
  await page.keyboard.press('o');
  await expect(page.getByTestId('overview')).toHaveCount(0);

  // N still toggles the notes.
  await page.keyboard.press('n');
  await expect(page.getByTestId('presenter-notes')).toHaveCount(0);
  await page.keyboard.press('n');
  await expect(page.getByTestId('presenter-notes')).toBeVisible();

  await settle(page, 300);
  expect(actions(api)).toEqual([]);
  await expect(page.getByTestId(IDS.state)).toHaveText('Draft');
});

test('PresenterPollPanel_WhenThePresenterMovesToThePollSlide_ShouldReadItAndShowIt', async ({
  mount,
  page,
}) => {
  const { api, hub } = await openPresenter(page, mount);
  await expect.poll(() => api.calls.length).toBe(1);

  await page.keyboard.press('ArrowRight');
  await expect(page.getByTestId('presenter-count')).toHaveText(`${POLL} / 16`);
  await expect(page.getByTestId(IDS.state)).toHaveText('Draft');
  expect(api.calls).toEqual([
    { call: 'read', slide: START },
    { call: 'read', slide: POLL },
  ]);
  expect(hub.updates).toEqual([{ slide: POLL, step: 0, black: false, skipDeepDives: false }]);
});
