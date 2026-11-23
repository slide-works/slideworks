import { test, expect } from '@playwright/experimental-ct-react';
import type { DeckPageView, LooksView } from '../api/types';
import { AppRoutes } from '../AppRoutes';
import {
  PALETTES,
  TEMPLATES,
  looksPaletteOf,
  looksTemplateOf,
} from '../test-support/fixtures/lookViewOf';
import { deckPage, me } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';

const route = { hooksConfig: { route: `/decks/${deckPage.id}` } };
const deckPath = `/api/decks/${deckPage.id}`;

const looks: LooksView = {
  templates: TEMPLATES.map(looksTemplateOf),
  palettes: PALETTES.map(looksPaletteOf),
};

test.beforeEach(async ({ page }) => {
  await page.route('**/api/looks', (route) => route.fulfill({ json: looks }));
  await page.route(`**${deckPage.look.logo!.url}`, (route) =>
    route.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
});

async function open(
  mount: Parameters<Parameters<typeof test>[2]>[0]['mount'],
  page: Parameters<Parameters<typeof test>[2]>[0]['page'],
  view: DeckPageView,
) {
  await stubApi(page, { '/api/me': { json: me }, [deckPath]: { json: view } });
  await mount(<AppRoutes />, route);
  await expect(page.getByTestId('deck-title')).toHaveText(view.title);
}

test('DeckPage_WhenWrite_ShouldShowEditPanelsWithoutShare', async ({ mount, page }) => {
  await open(mount, page, { ...deckPage, isOwner: false, canEdit: true, access: 'write' });

  await expect(page.getByTestId('look-panel')).toBeVisible();
  await expect(page.getByRole('region', { name: 'Q&A and reactions' })).toBeVisible();
  await expect(page.getByTestId('deck-access')).toHaveText('Can edit');
  await expect(page.getByTestId('deck-share')).toHaveCount(0);
});

test('DeckPage_WhenRead_ShouldShowNeither', async ({ mount, page }) => {
  await open(mount, page, { ...deckPage, isOwner: false, canEdit: false, access: 'read' });

  await expect(page.getByTestId('deck-access')).toHaveText('Can view');
  await expect(page.getByTestId('look-panel')).toHaveCount(0);
  await expect(page.getByRole('region', { name: 'Q&A and reactions' })).toHaveCount(0);
  await expect(page.getByTestId('deck-share')).toHaveCount(0);
});

test('DeckPage_WhenOwner_ShouldShowBoth', async ({ mount, page }) => {
  await open(mount, page, deckPage);

  await expect(page.getByTestId('look-panel')).toBeVisible();
  await expect(page.getByRole('region', { name: 'Q&A and reactions' })).toBeVisible();
  await expect(page.getByTestId('deck-share')).toBeVisible();
  await expect(page.getByTestId('deck-access')).toHaveCount(0);
});
