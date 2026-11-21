import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { AppRoutes } from '../AppRoutes';
import type { PresentationState } from '../presentation/types';
import { presentationState, presenterView } from '../test-support/fixtures/presenting';
import { me } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';
import { stubDeckHub, type StubPresentationOptions } from '../test-support/stubDeckHub';

const view = presenterView();
const deckId = view.id;
const route = { hooksConfig: { route: `/decks/${deckId}/present` } };
const presenterPath = `/api/decks/${deckId}/presenter`;
// The timer paused, so nothing on screen ticks.
const paused = (overrides: Partial<PresentationState> = {}) =>
  presentationState({ timer: { state: 'paused', elapsedMs: 760000 }, ...overrides });

// The deck's logo, drawn on every thumbnail.
test.beforeEach(async ({ page }) => {
  await page.route(`**${view.look.logo!.url}`, (route) =>
    route.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
});

// Stubs the API and the hub, mounts the presenter window and waits until it can be driven.
async function openPresenter(
  page: Page,
  mount: (component: React.JSX.Element, options: typeof route) => Promise<unknown>,
  presentation: StubPresentationOptions = { state: paused() },
) {
  const hub = await stubDeckHub(page, { presentation });
  await stubApi(page, { '/api/me': { json: me }, [presenterPath]: { json: view } });
  await mount(<AppRoutes />, route);
  return hub;
}

async function waitUntilLive(page: Page) {
  await expect(page.getByTestId('presenter-now')).toBeVisible();
  await expect(page.getByTestId('presenter-page')).toBeFocused();
}

// Lets the page run its pending work, so a call it should not make has had its chance.
const settle = (page: Page) => page.evaluate(() => new Promise((done) => setTimeout(done, 300)));

test('PresenterPage_WhenArrowSpaceAndPageKeysPressed_ShouldMoveThroughUpdatePresentation', async ({
  mount,
  page,
}) => {
  const hub = await openPresenter(page, mount);
  await waitUntilLive(page);
  await expect(page.getByTestId('presenter-count')).toHaveText('8 / 16');

  await page.keyboard.press('ArrowRight');
  await expect(page.getByTestId('presenter-count')).toHaveText('9 / 16');
  await page.keyboard.press(' ');
  await expect(page.getByTestId('presenter-count')).toHaveText('10 / 16');
  await page.keyboard.press('PageUp');
  await expect(page.getByTestId('presenter-count')).toHaveText('9 / 16');
  await page.keyboard.press('End');
  await expect(page.getByTestId('presenter-count')).toHaveText('16 / 16');
  await page.keyboard.press('Home');
  await expect(page.getByTestId('presenter-count')).toHaveText('1 / 16');

  expect(hub.updates).toEqual([
    { slide: 9, step: 0, black: false, skipDeepDives: false },
    { slide: 10, step: 0, black: false, skipDeepDives: false },
    { slide: 9, step: 0, black: false, skipDeepDives: false },
    { slide: 16, step: 0, black: false, skipDeepDives: false },
    { slide: 1, step: 0, black: false, skipDeepDives: false },
  ]);
  // The next button names where it goes.
  await expect(page.getByTestId('presenter-next-button')).toHaveAccessibleName(
    'Next: slide 2: Big batches hide the risk',
  );
});

test('PresenterPage_WhenSlideHasReveals_ShouldStepThemAndShowTheNextReveal', async ({
  mount,
  page,
}) => {
  const hub = await openPresenter(page, mount, { state: paused({ slide: 3, step: 0 }) });
  await waitUntilLive(page);
  await expect(page.getByTestId('presenter-next-label')).toHaveText('reveal 1 of 4');

  await page.keyboard.press('ArrowRight');
  await expect(page.getByTestId('presenter-position')).toHaveText('3.1 / 16');
  await expect(page.getByTestId('presenter-next-label')).toHaveText('reveal 2 of 4');
  expect(hub.updates).toEqual([{ slide: 3, step: 1, black: false, skipDeepDives: false }]);
});

test('PresenterPage_WhenSkipDeepDivesOn_ShouldNameTheSkippedSlideInNext', async ({
  mount,
  page,
}) => {
  const hub = await openPresenter(page, mount, { state: paused({ slide: 9 }) });
  await waitUntilLive(page);
  await expect(page.getByTestId('presenter-next-label')).toHaveText('slide 10 · deep dive');
  await expect(page.getByTestId('presenter-next-note')).toHaveText('2 deep dives ahead');

  await page.keyboard.press('d');

  await expect(page.getByTestId('presenter-tool-skip')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByTestId('presenter-next-label')).toHaveText('slide 11');
  await expect(page.getByTestId('presenter-next-note')).toHaveText(
    'Skipping deep dive 10: A story map shows the thinnest release',
  );
  expect(hub.updates).toEqual([{ slide: 9, step: 0, black: false, skipDeepDives: true }]);

  // Next steps over the deep dive; the count keeps the deck's numbers.
  await page.keyboard.press('ArrowRight');
  await expect(page.getByTestId('presenter-count')).toHaveText('11 / 16');
  await expect(page.getByTestId('presenter-next-note')).toHaveText(
    'Skipping deep dive 12: Lead time fell as the slices got smaller',
  );
});

test('PresenterPage_WhenBlackoutToggled_ShouldShowTheBlackBadgeFromTheState', async ({
  mount,
  page,
}) => {
  const hub = await openPresenter(page, mount);
  await waitUntilLive(page);
  await expect(page.getByTestId('presenter-black-badge')).toHaveCount(0);

  await page.keyboard.press('b');
  await expect(page.getByTestId('presenter-black-badge')).toContainText(
    'Audience sees a black screen',
  );
  await expect(page.getByTestId('presenter-tool-black')).toHaveAttribute('aria-pressed', 'true');

  await page.keyboard.press('.');
  await expect(page.getByTestId('presenter-black-badge')).toHaveCount(0);

  await page.getByTestId('presenter-tool-black').click();
  await expect(page.getByTestId('presenter-black-badge')).toBeVisible();
  expect(hub.updates.map((u) => u.black)).toEqual([true, false, true]);
  expect(hub.updates.every((u) => u.slide === 8 && u.step === 0)).toBe(true);
});

test('PresenterPage_WhenNotesToggledAndResized_ShouldHideThemAndKeepTheSize', async ({
  mount,
  page,
}) => {
  await openPresenter(page, mount);
  await waitUntilLive(page);
  const notes = page.getByTestId('presenter-notes');
  await expect(notes).toContainText("ask the room for last month's biggest story");

  await page.keyboard.press('n');
  await expect(notes).toHaveCount(0);
  await expect(page.getByTestId('presenter-tool-notes')).toHaveAttribute('aria-pressed', 'false');
  await page.keyboard.press('n');
  await expect(notes).toBeVisible();

  const body = page.getByTestId('presenter-notes-body');
  await expect(body).toHaveCSS('font-size', '18px');
  await page.getByRole('button', { name: 'Larger notes' }).click();
  await page.getByRole('button', { name: 'Larger notes' }).click();
  await expect(body).toHaveCSS('font-size', '22px');
  await page.getByRole('button', { name: 'Smaller notes' }).click();
  await expect(body).toHaveCSS('font-size', '20px');
  expect(await page.evaluate(() => localStorage.getItem('slideworks.presenter.notesSize'))).toBe(
    '20',
  );
});

test('PresenterPage_WhenSlideHasNoNotes_ShouldSaySo', async ({ mount, page }) => {
  const hub = await stubDeckHub(page, { presentation: { state: paused() } });
  const withoutNotes = presenterView();
  withoutNotes.slides[7].notes = null;
  await stubApi(page, { '/api/me': { json: me }, [presenterPath]: { json: withoutNotes } });
  await mount(<AppRoutes />, route);
  await waitUntilLive(page);

  await expect(page.getByTestId('presenter-notes')).toContainText('No notes for this slide.');
  expect(hub.updates).toEqual([]);
});

test('PresenterPage_WhenOPressed_ShouldOpenOverviewAndEscShouldCloseIt', async ({
  mount,
  page,
}) => {
  const hub = await openPresenter(page, mount);
  await waitUntilLive(page);

  await page.keyboard.press('o');
  await expect(page.getByTestId('overview')).toBeVisible();
  await expect(page.getByTestId('presenter-tool-overview')).toHaveAttribute('aria-pressed', 'true');
  // The overview's keys are its own: arrows don't move the talk while it is open.
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('overview')).toHaveCount(0);
  await expect(page.getByTestId('presenter-tool-overview')).toBeFocused();
  await settle(page);
  expect(hub.updates).toEqual([]);

  // A slide picked in the overview is a jump: step 0, even for a deep dive.
  await page.keyboard.press('o');
  await page.getByTestId('overview-slide-12').click();
  await expect(page.getByTestId('overview')).toHaveCount(0);
  await expect(page.getByTestId('presenter-count')).toHaveText('12 / 16');
  expect(hub.updates).toEqual([{ slide: 12, step: 0, black: false, skipDeepDives: false }]);
});

test('PresenterPage_WhenACheckboxHasFocus_ShouldStillMoveWithTheArrowsAndLeaveSpaceToIt', async ({
  mount,
  page,
}) => {
  const hub = await openPresenter(page, mount);
  await waitUntilLive(page);
  await page.evaluate(() => {
    const box = document.createElement('input');
    box.type = 'checkbox';
    box.id = 'probe-checkbox';
    document.querySelector('[data-testid="presenter-page"]')!.appendChild(box);
    box.focus();
  });
  await expect(page.locator('#probe-checkbox')).toBeFocused();

  await page.keyboard.press('ArrowRight');
  await expect(page.getByTestId('presenter-count')).toHaveText('9 / 16');
  await page.keyboard.press('ArrowLeft');
  await expect(page.getByTestId('presenter-count')).toHaveText('8 / 16');
  // Space toggles the checkbox and does not move the talk.
  await page.keyboard.press(' ');
  await expect(page.locator('#probe-checkbox')).toBeChecked();
  await settle(page);
  expect(hub.updates).toHaveLength(2);
});

test('PresenterPage_WhenNumberThenEnterTyped_ShouldJumpToThatSlide', async ({ mount, page }) => {
  const hub = await openPresenter(page, mount);
  await waitUntilLive(page);

  await page.keyboard.press('1');
  await page.keyboard.press('2');
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('presenter-count')).toHaveText('12 / 16');

  // A number past the end jumps nowhere.
  await page.keyboard.press('4');
  await page.keyboard.press('0');
  await page.keyboard.press('Enter');
  await settle(page);
  expect(hub.updates).toEqual([{ slide: 12, step: 0, black: false, skipDeepDives: false }]);
});

test('PresenterPage_WhenTPressed_ShouldResetTheTimer', async ({ mount, page }) => {
  const hub = await openPresenter(page, mount);
  await waitUntilLive(page);

  await page.keyboard.press('t');
  await expect.poll(() => hub.timers).toEqual(['reset']);
  await expect(page.getByTestId('presenter-timer')).toHaveAttribute('data-state', 'notStarted');
});

test('PresenterPage_WhenConnectionDrops_ShouldShowTheReconnectingBanner', async ({
  mount,
  page,
}) => {
  const hub = await openPresenter(page, mount, { state: paused(), holdJoin: true });
  await expect.poll(() => hub.presentationJoins.length).toBe(1);
  hub.releasePresentationJoins();
  await waitUntilLive(page);
  await expect(page.getByTestId('presenter-reconnecting')).toHaveCount(0);

  hub.drop();

  await expect(page.getByTestId('presenter-reconnecting')).toHaveText(
    "Reconnecting · the audience window won't follow until it's back",
  );
  await expect.poll(() => hub.presentationJoins.length).toBe(2);
  hub.releasePresentationJoins();
  await expect(page.getByTestId('presenter-reconnecting')).toHaveCount(0);
});

test('PresenterPage_WhenNoAudienceWindowConnected_ShouldOfferToReopenIt', async ({
  mount,
  page,
}) => {
  const state = paused();
  const hub = await openPresenter(page, mount, {
    state: { ...state, connected: state.connected.filter((c) => c.role !== 'audience') },
  });
  await waitUntilLive(page);
  await page.evaluate(() => {
    const opened: string[][] = [];
    Object.assign(window, { opened });
    window.open = (url?: string | URL, target?: string) => {
      opened.push([String(url), target ?? '']);
      return null;
    };
  });

  await expect(page.getByTestId('presenter-audience-closed')).toContainText(
    'Audience window closed',
  );
  await page.getByTestId('presenter-audience-reopen').click();
  expect(await page.evaluate(() => (window as unknown as { opened: string[][] }).opened)).toEqual([
    [view.audienceUrl, `sw-audience-${deckId}`],
  ]);

  // The audience window joining clears the banner.
  hub.pushState(paused({ seq: 43 }));
  await expect(page.getByTestId('presenter-audience-closed')).toHaveCount(0);
});

test('PresenterPage_WhenPhoneButtonClicked_ShouldOpenTheDialogWithTheRemoteUrl', async ({
  mount,
  page,
}) => {
  await openPresenter(page, mount);
  await waitUntilLive(page);
  await expect(page.getByTestId('presenter-tool-phone')).toHaveText('Phone connected');

  await page.getByTestId('presenter-tool-phone').click();
  await expect(page.getByTestId('phone-connect-url')).toHaveText(view.remoteUrl);
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('phone-connect')).toHaveCount(0);
  await expect(page.getByTestId('presenter-tool-phone')).toBeFocused();
});

test('PresenterPage_WhenAMoveFails_ShouldShowAMessage', async ({ mount, page }) => {
  const hub = await openPresenter(page, mount);
  await waitUntilLive(page);
  hub.fail('UpdatePresentation', 'Not presenting');

  await page.keyboard.press('ArrowRight');

  await expect(page.getByTestId('presenter-error')).toHaveText(
    "Couldn't move the presentation. Check your connection and try again.",
  );
  await expect(page.getByTestId('presenter-count')).toHaveText('8 / 16');
  await page.keyboard.press('ArrowRight');
  await expect(page.getByTestId('presenter-error')).toHaveCount(0);
  await expect(page.getByTestId('presenter-count')).toHaveText('9 / 16');
});

test('PresenterPage_WhenTheJoinIsRefused_ShouldShowNotFound', async ({ mount, page }) => {
  const hub = await openPresenter(page, mount, { state: paused(), refuse: true });

  await expect(page.getByTestId('presenter-not-found')).toHaveText('Deck not found');
  await settle(page);
  expect(hub.updates).toEqual([]);
});

test('PresenterPage_WhenTheViewIs404_ShouldShowNotFound', async ({ mount, page }) => {
  await stubDeckHub(page, { presentation: { state: paused() } });
  await stubApi(page, { '/api/me': { json: me }, [presenterPath]: { status: 404 } });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('presenter-not-found')).toBeVisible();
});

test('PresenterPage_WhenTheDeckChanges_ShouldReadItAgainAndKeepThePosition', async ({
  mount,
  page,
}) => {
  const hub = await openPresenter(page, mount);
  await waitUntilLive(page);
  const edited = presenterView();
  edited.slides[7].notes = 'Edited by an agent mid-talk.';
  await page.unroute(`**${presenterPath}`);
  await stubApi(page, { [presenterPath]: { json: edited } });

  hub.push({ type: 'DeckChanged', deckId, revision: 2 });

  await expect(page.getByTestId('presenter-notes')).toContainText('Edited by an agent mid-talk.');
  await expect(page.getByTestId('presenter-count')).toHaveText('8 / 16');
});

test('PresenterPage default view screenshot', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await openPresenter(page, mount);
  await waitUntilLive(page);
  await expect(page.getByTestId('presenter-thumb').first()).toBeVisible();
  await expect(page).toHaveScreenshot();
});
