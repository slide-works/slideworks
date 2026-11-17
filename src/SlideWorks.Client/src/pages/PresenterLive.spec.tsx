import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { PresenterView } from '../api/types';
import { AppRoutes } from '../AppRoutes';
import type { LiveView } from '../live/liveView';
import type { PresentationState } from '../presentation/types';
import { presentationState, presenterView } from '../test-support/fixtures/presenting';
import { me } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';
import { stubDeckHub } from '../test-support/stubDeckHub';

const base = presenterView();
const deckId = base.id;
const route = { hooksConfig: { route: `/decks/${deckId}/present` } };
const liveUrl = `**/api/decks/${deckId}/live`;
const JOIN_SLIDE = 9;

const idle: LiveView = {
  sessionId: null,
  code: base.join.code,
  codeLabel: base.join.codeLabel,
  joinUrl: base.join.joinUrl,
  hostLabel: base.join.hostLabel,
  live: false,
  joinedCount: 0,
  joinBar: false,
};
const open: LiveView = { ...idle, sessionId: 'session-1', live: true, joinedCount: 7 };

// The talk paused, so nothing on screen ticks.
const paused = (overrides: Partial<PresentationState> = {}) =>
  presentationState({ timer: { state: 'paused', elapsedMs: 760000 }, ...overrides });

// The deck with slide 9 as its join slide, live or not as the deck's join says.
function withJoinSlide(live: boolean): PresenterView {
  const view = presenterView();
  view.slides[JOIN_SLIDE - 1] = { ...view.slides[JOIN_SLIDE - 1], layout: 'join', slots: {} };
  view.join = { ...view.join, live };
  return view;
}

type Server = { view: PresenterView; session: LiveView; calls: string[] };

// Plays the presenter view and /live at the network boundary. The session is the server's: Go live and End change it,
// and a later read of the presenter view carries its live state, as the real server's does.
async function stubServer(page: Page, session: LiveView, joinSlide = false): Promise<Server> {
  const server: Server = {
    view: joinSlide ? withJoinSlide(session.live) : presenterView(),
    session,
    calls: [],
  };
  server.view.join = { ...server.view.join, live: session.live };
  await page.route(`**/api/decks/${deckId}/presenter`, (request) => {
    server.view = { ...server.view, join: { ...server.view.join, live: server.session.live } };
    return request.fulfill({ json: server.view });
  });
  await page.route(liveUrl, (request) => {
    const method = request.request().method();
    server.calls.push(method);
    if (method === 'POST') server.session = { ...open, joinedCount: 0 };
    if (method === 'DELETE') {
      server.session = { ...idle };
      return request.fulfill({ status: 204 });
    }
    return request.fulfill({ json: server.session });
  });
  await stubApi(page, { '/api/me': { json: me } });
  return server;
}

async function openPresenter(
  page: Page,
  mount: (component: React.JSX.Element, options: typeof route) => Promise<unknown>,
  session: LiveView,
  { joinSlide = false, state = paused() }: { joinSlide?: boolean; state?: PresentationState } = {},
) {
  const hub = await stubDeckHub(page, { presentation: { state } });
  const server = await stubServer(page, session, joinSlide);
  await mount(<AppRoutes />, route);
  await expect(page.getByTestId('presenter-now')).toBeVisible();
  return { hub, server };
}

const chip = (page: Page) => page.getByTestId('presenter-live-chip');

test.beforeEach(async ({ page }) => {
  await page.route(`**${base.look.logo!.url}`, (request) =>
    request.fulfill({
      path: 'src/test-support/fixtures/sample-image.png',
      contentType: 'image/png',
    }),
  );
});

test('PresenterPage_WhenOwnerNotLive_ShouldOfferGoLiveInTheTopBar', async ({ mount, page }) => {
  await openPresenter(page, mount, idle);

  await expect(chip(page).getByRole('button', { name: `Go live for ${base.title}` })).toBeVisible();
  await expect(page.getByTestId('live-chip-end')).toHaveCount(0);
});

test('PresenterPage_WhenGoLiveClickedAndEndClicked_ShouldShowThePillThenGoLiveAgain', async ({
  mount,
  page,
}) => {
  const { server } = await openPresenter(page, mount, idle);

  await page.getByRole('button', { name: `Go live for ${base.title}` }).click();
  await expect(page.getByTestId('live-chip-code')).toHaveText(base.join.codeLabel);
  await expect(page.getByTestId('live-chip-count')).toHaveText('0 joined');
  await expect(page.getByTestId('live-chip-join-bar')).toBeVisible();
  await page.getByRole('button', { name: `End live session for ${base.title}` }).click();

  await expect(page.getByTestId('live-chip-go-live')).toBeVisible();
  await expect(page.getByTestId('live-chip-end')).toHaveCount(0);
  expect(server.calls).toEqual(['GET', 'POST', 'DELETE']);
});

test('PresenterPage_WhenDeckIsLive_ShouldShowThePillWithTheCodeAndTheJoinedCount', async ({
  mount,
  page,
}) => {
  await openPresenter(page, mount, open);

  await expect(page.getByTestId('live-chip-code')).toHaveText(base.join.codeLabel);
  await expect(page.getByTestId('live-chip-count')).toHaveText('7 joined');
  await expect(page.getByTestId('live-chip-join-bar')).toHaveAttribute('aria-pressed', 'false');
});

test('PresenterPage_WhenGoLiveFails_ShouldShowAMessage', async ({ mount, page }) => {
  await openPresenter(page, mount, idle);
  await page.route(liveUrl, (request) =>
    request.request().method() === 'POST'
      ? request.fulfill({ status: 500, json: { error: 'boom' } })
      : request.fallback(),
  );

  await page.getByTestId('live-chip-go-live').click();

  await expect(page.getByTestId('live-chip-error')).toHaveText('Could not go live.');
  await expect(page.getByTestId('live-chip-go-live')).toBeVisible();
});

test('PresenterPage_WhenAnotherWindowEndsTheSession_ShouldShowGoLiveAgain', async ({
  mount,
  page,
}) => {
  const { hub, server } = await openPresenter(page, mount, open);
  await expect(page.getByTestId('live-chip-end')).toBeVisible();

  server.session = { ...idle };
  hub.push({ type: 'liveChanged', deckId });

  await expect(page.getByTestId('live-chip-go-live')).toBeVisible();
});

test('PresenterPage_WhenAnotherWindowGoesLive_ShouldShowThePill', async ({ mount, page }) => {
  const { hub, server } = await openPresenter(page, mount, idle);
  await expect(page.getByTestId('live-chip-go-live')).toBeVisible();

  server.session = { ...open };
  hub.push({ type: 'liveChanged', deckId });

  await expect(page.getByTestId('live-chip-code')).toHaveText(base.join.codeLabel);
  await expect(page.getByTestId('live-chip-count')).toHaveText('7 joined');
});

test('PresenterPage_WhenCurrentSlideIsAJoinSlideAndLive_ShouldShowTheCodeAndTheCount', async ({
  mount,
  page,
}) => {
  await openPresenter(page, mount, open, {
    joinSlide: true,
    state: paused({ slide: JOIN_SLIDE }),
  });

  const now = page.getByTestId('presenter-now');
  await expect(now.getByTestId('join-code')).toHaveText(base.join.codeLabel);
  await expect(now.getByTestId('join-qr')).toBeVisible();
  await expect(now.getByTestId('join-count')).toContainText('7 joined');
  await expect(now.getByTestId('join-not-live')).toHaveCount(0);
});

test('PresenterPage_WhenCurrentSlideIsAJoinSlideAndNotLive_ShouldSayNotLiveYet', async ({
  mount,
  page,
}) => {
  await openPresenter(page, mount, idle, { joinSlide: true, state: paused({ slide: JOIN_SLIDE }) });

  await expect(page.getByTestId('presenter-now').getByTestId('join-not-live')).toContainText(
    'Not live yet',
  );
});

test('PresenterPage_WhenTheNextSlideIsAJoinSlideAndLive_ShouldShowItLiveInThePreview', async ({
  mount,
  page,
}) => {
  await openPresenter(page, mount, open, {
    joinSlide: true,
    state: paused({ slide: JOIN_SLIDE - 1 }),
  });

  const next = page.getByTestId('presenter-next');
  await expect(next.getByTestId('join-code')).toHaveText(base.join.codeLabel);
  await expect(next.getByTestId('join-not-live')).toHaveCount(0);
});

test('PresenterPage_WhenOverviewOpenAndLive_ShouldShowTheJoinSlideLive', async ({
  mount,
  page,
}) => {
  await openPresenter(page, mount, open, { joinSlide: true });

  await page.keyboard.press('o');

  const thumb = page.getByTestId(`overview-slide-${JOIN_SLIDE}`);
  await expect(thumb.getByTestId('join-code')).toHaveText(base.join.codeLabel);
  await expect(thumb.getByTestId('join-not-live')).toHaveCount(0);
});

test('PresenterPage_WhenJoinBarToggled_ShouldSendNoMove', async ({ mount, page }) => {
  const { hub } = await openPresenter(page, mount, open, { state: paused({ slide: 12 }) });

  // A chip control is not a move: the window's position and the presentation stay as they were.
  await page.getByTestId('live-chip-join-bar').click();
  await expect(page.getByTestId('presenter-count')).toHaveText('12 / 16');
  expect(hub.updates).toEqual([]);
});

test('PresenterTopBar with the live chip not live screenshot', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await openPresenter(page, mount, idle);
  await expect(page.getByTestId('live-chip-go-live')).toBeVisible();
  await expect(page.getByTestId('presenter-top-bar')).toHaveScreenshot();
});

test('PresenterTopBar with the live chip live screenshot', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await openPresenter(page, mount, open);
  await expect(page.getByTestId('live-chip-count')).toHaveText('7 joined');
  await expect(page.getByTestId('presenter-top-bar')).toHaveScreenshot();
});
