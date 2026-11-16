import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { DeckPageView, LooksView, SharingRequest } from '../api/types';
import { AppRoutes } from '../AppRoutes';
import { GoTo } from '../test-support/GoTo';
import { LocationProbe } from '../test-support/LocationProbe';
import {
  PALETTES,
  TEMPLATES,
  looksPaletteOf,
  looksTemplateOf,
} from '../test-support/fixtures/lookViewOf';
import { deckPage, me, sharing } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';
import { stubDeckHub } from '../test-support/stubDeckHub';

const route = { hooksConfig: { route: `/decks/${deckPage.id}` } };
const deckPath = `/api/decks/${deckPage.id}`;

// The owner's deck page shows the Look panel, which reads the catalogue from GET /api/looks
// and shows the deck's logo.
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

// A response held until release(), to land answers out of order.
function gate() {
  let release!: () => void;
  const opened = new Promise<void>((resolve) => {
    release = resolve;
  });
  return { opened, release };
}

// Lets the page settle after a held response is released: the fetch resolves,
// its JSON is parsed and React renders.
const settle = (page: Page) => page.evaluate(() => new Promise((done) => setTimeout(done, 200)));

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

  // Thumbnail footers carry the deck's chrome, as the player's do: this
  // deck's template shows the server's date label in place of the author.
  await expect(thumbs.nth(1).getByTestId('slide-footer-text')).toHaveText(
    '2 / 3 · Ship smaller · 16 Nov 2026',
  );

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

test('deck page re-reads on DeckChanged', async ({ mount, page }) => {
  const hub = await stubDeckHub(page);
  const longer: DeckPageView = {
    ...deckPage,
    slides: [
      ...deckPage.slides,
      { ...deckPage.slides[1], number: 4, link: `${deckPage.playUrl}#4` },
    ],
  };
  let reads = 0;
  await stubApi(page, { '/api/me': { json: me } });
  await page.route(`**${deckPath}`, (route) => {
    reads += 1;
    return route.fulfill({ json: reads === 1 ? deckPage : longer });
  });
  await mount(<AppRoutes />, route);

  await expect(page.getByTestId('deck-thumb')).toHaveCount(3);
  await expect.poll(() => hub.joined).toEqual([deckPage.id]);
  await page.getByTestId('deck-thumb-select').nth(1).click();

  hub.push({ type: 'deckChanged', deckId: 'Zz9OtherDeck' });
  hub.push({ type: 'deckChanged', deckId: deckPage.id });

  await expect(page.getByTestId('deck-thumb')).toHaveCount(4);
  await expect(page.getByTestId('deck-slide-count')).toHaveText('4 slides');
  expect(reads).toBe(2);
  // The selected slide survives the re-read.
  await expect(page.getByTestId('selected-number')).toHaveText('2');
});

test('deck page keeps the newest deck when an older re-read answers last', async ({
  mount,
  page,
}) => {
  const hub = await stubDeckHub(page);
  const longer: DeckPageView = {
    ...deckPage,
    slides: [
      ...deckPage.slides,
      { ...deckPage.slides[1], number: 4, link: `${deckPage.playUrl}#4` },
    ],
  };
  const held = gate();
  let reads = 0;
  await stubApi(page, { '/api/me': { json: me } });
  await page.route(`**${deckPath}`, async (route) => {
    reads += 1;
    if (reads === 2) {
      // The older re-read: the deck before slide 4 was added, answering last.
      await held.opened;
      return route.fulfill({ json: deckPage });
    }
    return route.fulfill({ json: reads === 1 ? deckPage : longer });
  });
  await mount(<AppRoutes />, route);
  await expect(page.getByTestId('deck-thumb')).toHaveCount(3);
  await expect.poll(() => hub.joined).toEqual([deckPage.id]);

  hub.push({ type: 'deckChanged', deckId: deckPage.id });
  await expect.poll(() => reads).toBe(2);
  hub.push({ type: 'deckChanged', deckId: deckPage.id });
  await expect(page.getByTestId('deck-thumb')).toHaveCount(4);

  held.release();
  await expect.poll(() => reads).toBe(3);
  await settle(page);

  await expect(page.getByTestId('deck-thumb')).toHaveCount(4);
  await expect(page.getByTestId('deck-slide-count')).toHaveText('4 slides');
});

test('deck page ignores a re-read for the deck it moved away from', async ({ mount, page }) => {
  const hub = await stubDeckHub(page);
  const other: DeckPageView = { ...deckPage, id: 'Bb3OtherDeck', slug: 'other', title: 'Other' };
  const held = gate();
  let released = false;
  let reads = 0;
  await stubApi(page, { '/api/me': { json: me }, [`/api/decks/${other.id}`]: { json: other } });
  await page.route(`**${deckPath}`, async (route) => {
    reads += 1;
    if (reads === 2) {
      await held.opened;
      await route.fulfill({ json: deckPage });
      released = true;
      return;
    }
    return route.fulfill({ json: deckPage });
  });
  await mount(
    <GoTo>
      <AppRoutes />
    </GoTo>,
    route,
  );
  await expect(page.getByTestId('deck-title')).toHaveText(deckPage.title);
  await expect.poll(() => hub.joined).toEqual([deckPage.id]);

  // The deck changes, and while its re-read is still out, the owner opens another deck.
  hub.push({ type: 'deckChanged', deckId: deckPage.id });
  await expect.poll(() => reads).toBe(2);
  await page.getByTestId('goto-path').fill(`/decks/${other.id}`);
  await page.getByTestId('goto').click();
  await expect(page.getByTestId('deck-title')).toHaveText('Other');

  held.release();
  await expect.poll(() => released).toBe(true);
  await settle(page);

  await expect(page.getByTestId('deck-page')).toHaveAttribute('data-deck-id', other.id);
  await expect(page.getByTestId('deck-title')).toHaveText('Other');
});

test('saving in the sharing dialog re-reads the deck page once', async ({ mount, page }) => {
  const after: DeckPageView = { ...deckPage, sharing: { level: 'named', viewerCount: 2 } };
  const puts: SharingRequest[] = [];
  let reads = 0;
  await stubApi(page, { '/api/me': { json: me } });
  await page.route(`**${deckPath}`, (route) => {
    reads += 1;
    return route.fulfill({ json: reads === 1 ? deckPage : after });
  });
  await page.route(`**${deckPath}/sharing`, (route) => {
    if (route.request().method() !== 'PUT') {
      return route.fulfill({ json: { ...sharing, level: 'link' } });
    }
    const body = route.request().postDataJSON() as SharingRequest;
    puts.push(body);
    return route.fulfill({ json: { ...sharing, ...body } });
  });
  await mount(<AppRoutes />, route);
  await expect(page.getByTestId('deck-sharing-level')).toHaveText('Anyone with the link');

  await page.getByTestId('deck-share').click();
  const dialog = page.getByTestId('sharing-dialog');
  await dialog.getByRole('radio', { name: 'Named people' }).check();
  await dialog.getByTestId('sharing-save').click();

  await expect(page.getByTestId('deck-sharing-level')).toHaveText('2 named viewers');
  await expect(dialog).toHaveCount(0);
  await expect(page.getByTestId('deck-share')).toBeFocused();
  expect(puts).toEqual([{ level: 'named', viewers: sharing.viewers }]);
  await settle(page);
  expect(reads).toBe(2);
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
