import { test, expect } from '@playwright/experimental-ct-react';
import type { DeckPageView } from '../api/types';
import { AppRoutes } from '../AppRoutes';
import { LocationProbe } from '../test-support/LocationProbe';
import { deckPage, me } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';

const route = { hooksConfig: { route: `/decks/${deckPage.id}` } };
const deckPath = `/api/decks/${deckPage.id}`;

test('deck page grid shows thumbnails and notes of clicked slide', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me }, [deckPath]: { json: deckPage } });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('deck-title')).toHaveText('Ship smaller');
  await expect(page.getByTestId('deck-slide-count')).toHaveText('3 slides');
  await expect(page.getByTestId('deck-sharing-level')).toHaveText('Anyone with the link');

  const thumbs = page.getByTestId('deck-thumb');
  await expect(thumbs).toHaveCount(3);
  for (const slide of deckPage.slides) {
    await expect(thumbs.nth(slide.number - 1).getByTestId(`slide-${slide.number}`)).toHaveAttribute(
      'data-layout',
      slide.layout,
    );
  }

  // The first slide is selected to begin with.
  await expect(page.getByTestId('selected-number')).toHaveText('1');
  await expect(page.getByTestId('selected-notes')).toHaveText(deckPage.slides[0].notes!);

  await thumbs.nth(1).getByTestId('deck-thumb-select').click();
  await expect(thumbs.nth(1).getByTestId('deck-thumb-select')).toHaveAttribute(
    'aria-pressed',
    'true',
  );
  await expect(page.getByTestId('selected-number')).toHaveText('2');
  await expect(page.getByTestId('selected-layout')).toHaveText('practice');
  await expect(page.getByTestId('selected-steps')).toHaveText('0');
  await expect(page.getByTestId('selected-notes')).toHaveText(
    "Say: ask the room for last month's biggest story, and slice it live on the whiteboard.",
  );
  await expect(page.getByTestId('selected-link')).toHaveText('/d/7Kq2xYp9LmQa/ship-smaller#2');
  await expect(page.getByTestId('selected-link')).toHaveAttribute(
    'href',
    '/d/7Kq2xYp9LmQa/ship-smaller#2',
  );
});

test('a slide without notes says so', async ({ mount, page }) => {
  const view = {
    ...deckPage,
    slides: deckPage.slides.map((slide) => ({ ...slide, notes: null })),
  } as DeckPageView;
  await stubApi(page, { '/api/me': { json: me }, [deckPath]: { json: view } });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('selected-notes')).toHaveText('No speaker notes.');
});

test('activity shows agent entries', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me }, [deckPath]: { json: deckPage } });
  await mount(<AppRoutes />, route);

  const rows = page.getByTestId('activity-row');
  await expect(rows).toHaveCount(3);
  await expect(rows.nth(0)).toContainText(
    'Claude added slide 3 close · 2 min ago · via MCP add_slide',
  );
  await expect(rows.nth(1)).toContainText(
    'Claude changed sharing: Private → Anyone with the link · 5 min ago · via MCP set_sharing',
  );
  await expect(rows.nth(2)).toContainText('Claude created the deck · 9 min ago · via MCP');
});

test("a person's change on the owner's page reads You", async ({ mount, page }) => {
  const view: DeckPageView = {
    ...deckPage,
    activity: [
      {
        actor: 'Sam Rivera',
        actorKind: 'person',
        via: 'web',
        tool: null,
        summary: 'created the deck',
        atLabel: 'just now',
      },
    ],
  };
  await stubApi(page, { '/api/me': { json: me }, [deckPath]: { json: view } });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('activity-row')).toContainText(
    'You created the deck · just now · in the browser',
  );
});

test('share button opens dialog', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me }, [deckPath]: { json: deckPage } });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('sharing-dialog')).toHaveCount(0);
  await page.getByTestId('deck-share').click();
  await expect(page.getByTestId('sharing-dialog')).toBeVisible();
});

test('present opens the player, later features are disabled', async ({ mount, page }) => {
  await stubApi(page, { '/api/me': { json: me }, [deckPath]: { json: deckPage } });
  await mount(
    <LocationProbe>
      <AppRoutes />
    </LocationProbe>,
    route,
  );

  const later = page.getByTestId('deck-later');
  await expect(later).toHaveCount(6);
  for (const [index, feature] of ['SW-2', 'SW-3', 'SW-4', 'SW-14', 'SW-14', 'SW-13'].entries()) {
    await expect(later.nth(index)).toBeDisabled();
    await expect(later.nth(index)).toHaveAttribute('title', `Coming later (${feature})`);
  }

  await expect(page.getByTestId('deck-present')).toHaveAttribute('href', deckPage.playUrl);
  await page.getByTestId('deck-present').click();
  await expect(page.getByTestId('location')).toHaveText(deckPage.playUrl);
});

test('deck not found', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/me': { json: me },
    [deckPath]: { status: 404, json: { error: 'Deck not found.' } },
  });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('deck-not-found')).toHaveText('Deck not found');
  await expect(page.getByTestId('deck-thumb')).toHaveCount(0);
});

test('api error shows message', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/me': { json: me },
    [deckPath]: { status: 500, json: { error: 'Something went wrong.' } },
  });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('deck-error')).toHaveText('Something went wrong.');
});

// The baseline comes from CI's playwright job (never from a dev machine); E2
// refreshes the baselines from its report.
test('deck page screenshot', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await stubApi(page, { '/api/me': { json: me }, [deckPath]: { json: deckPage } });
  await mount(<AppRoutes />, route);
  await expect(page.getByTestId('deck-thumb')).toHaveCount(3);
  await expect(page.getByTestId('activity-row')).toHaveCount(3);
  await expect(page).toHaveScreenshot('deck-page.png');
});
