import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { DeckPlayView } from '../api/types';
import { AppRoutes } from '../AppRoutes';
import type { LiveView } from '../live/liveView';
import { presentationState, presenterView } from '../test-support/fixtures/presenting';
import { deckPlay, me } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';
import { stubDeckHub } from '../test-support/stubDeckHub';

// A join slide must show the code and the live state wherever a window draws slides (SW-5 D19): the audience window and
// the phone's speaker mode, besides the presenter window's own panels (PresenterLive.spec).

const JOIN_SLIDE = 2;
const open: LiveView = {
  sessionId: 'session-1',
  code: deckPlay.join.code,
  codeLabel: deckPlay.join.codeLabel,
  joinUrl: deckPlay.join.joinUrl,
  hostLabel: deckPlay.join.hostLabel,
  live: true,
  joinedCount: 7,
  joinBar: false,
};

function playWithJoinSlide(isOwner: boolean): DeckPlayView {
  const slides = deckPlay.slides.map((slide) =>
    slide.number === JOIN_SLIDE ? { ...slide, layout: 'join' as const, slots: {} } : slide,
  );
  return { ...deckPlay, slides, isOwner, join: { ...deckPlay.join, live: true } };
}

async function openAudienceWindow(page: Page, isOwner: boolean) {
  await stubApi(page, { [`/api/play/${deckPlay.id}`]: { json: playWithJoinSlide(isOwner) } });
  await page.route(`**/api/decks/${deckPlay.id}/live`, (request) =>
    isOwner
      ? request.fulfill({ json: open })
      : request.fulfill({ status: 403, json: { error: "Only the deck's owner can go live" } }),
  );
  await stubDeckHub(page, {
    presentation: {
      state: presentationState({
        deckId: deckPlay.id,
        slide: JOIN_SLIDE,
        step: 0,
        connected: [{ clientId: 'k-1', role: 'presenter', label: 'Chrome on Linux' }],
      }),
    },
  });
}

const audienceRoute = `/d/${deckPlay.id}/${deckPlay.slug}?present`;

test('AudienceWindow_WhenOwnerAndLive_ShouldShowTheJoinSlideLiveWithTheCount', async ({
  mount,
  page,
}) => {
  await openAudienceWindow(page, true);
  await mount(<AppRoutes />, { hooksConfig: { route: audienceRoute } });

  await expect(page.getByTestId('join-code')).toHaveText(deckPlay.join.codeLabel);
  await expect(page.getByTestId('join-qr')).toBeVisible();
  await expect(page.getByTestId('join-count')).toContainText('7 joined');
  await expect(page.getByTestId('join-not-live')).toHaveCount(0);
  await expect(page.getByTestId('live-chip')).toHaveCount(0);
});

test('AudienceWindow_WhenSignedOutVenuePc_ShouldShowTheJoinSlideLiveWithoutACount', async ({
  mount,
  page,
}) => {
  await openAudienceWindow(page, false);
  await mount(<AppRoutes />, { hooksConfig: { route: audienceRoute } });

  await expect(page.getByTestId('join-code')).toHaveText(deckPlay.join.codeLabel);
  await expect(page.getByTestId('join-not-live')).toHaveCount(0);
  await expect(page.getByTestId('join-count')).toHaveCount(0);
});

test('SpeakerMode_WhenOnAJoinSlideAndLive_ShouldShowTheCodeAndTheCount', async ({
  mount,
  page,
}) => {
  const view = presenterView();
  view.slides[7] = { ...view.slides[7], layout: 'join', slots: {} };
  view.join = { ...view.join, live: true };
  await stubApi(page, {
    '/api/me': { json: me },
    [`/api/decks/${view.id}/presenter`]: { json: view },
  });
  await page.route(`**/api/decks/${view.id}/live`, (request) => request.fulfill({ json: open }));
  await page.route(`**${view.look.logo!.url}`, (request) =>
    request.fulfill({
      path: 'src/test-support/fixtures/sample-image.png',
      contentType: 'image/png',
    }),
  );
  await stubDeckHub(page, {
    presentation: {
      state: presentationState({ slide: 8, timer: { state: 'paused', elapsedMs: 760000 } }),
    },
  });
  await mount(<AppRoutes />, { hooksConfig: { route: `/remote/${view.id}` } });

  await expect(page.getByTestId('join-code').first()).toHaveText(view.join.codeLabel);
  await expect(page.getByTestId('join-not-live')).toHaveCount(0);
});
