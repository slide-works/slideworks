import { test, expect } from '@playwright/experimental-ct-react';
import type { DecksLibraryView, LooksView } from '../api/types';
import { LocationProbe } from '../test-support/LocationProbe';
import {
  PALETTES,
  TEMPLATES,
  looksPaletteOf,
  looksTemplateOf,
} from '../test-support/fixtures/lookViewOf';
import { decksLibrary } from '../test-support/fixtures/screens';
import { stubApi } from '../test-support/stubApi';
import { DecksPage } from './DecksPage';

const route = { hooksConfig: { route: '/decks' } };

// The built-in look catalogue the New deck dialog reads.
const looks: LooksView = {
  templates: TEMPLATES.map(looksTemplateOf),
  palettes: PALETTES.map(looksPaletteOf),
};

test('lists decks with last change by agent via mcp', async ({ mount, page }) => {
  await stubApi(page, { '/api/decks': { json: decksLibrary } });
  await mount(<DecksPage />, route);

  const rows = page.getByTestId('decks-table').getByTestId('deck-row');
  await expect(rows).toHaveCount(2);
  await expect(page.getByTestId('decks-count')).toHaveText('2');

  const shipSmaller = rows.nth(0);
  await expect(shipSmaller.getByTestId('deck-title')).toHaveText('Ship smaller');
  await expect(shipSmaller.getByTestId('deck-title')).toHaveAttribute(
    'href',
    '/decks/7Kq2xYp9LmQa',
  );
  await expect(shipSmaller).toContainText('Team offsite · draft');
  await expect(shipSmaller.getByRole('cell').nth(1)).toHaveText('16');
  await expect(shipSmaller.getByTestId('deck-last-change')).toHaveText(
    'Claude via MCP · added slide 16',
  );
  await expect(shipSmaller.getByTestId('deck-sharing')).toHaveText('Link');
  await expect(shipSmaller.getByTestId('deck-updated')).toHaveText('2 min ago');
  await expect(shipSmaller.getByTestId('deck-present')).toHaveText('▶ Present');
  await expect(shipSmaller.getByTestId('deck-present')).toHaveAttribute(
    'href',
    '/d/7Kq2xYp9LmQa/ship-smaller',
  );

  const review = rows.nth(1);
  await expect(review.getByTestId('deck-last-change')).toHaveText('You · changed sharing');
  await expect(review.getByTestId('deck-sharing')).toHaveText('3 viewers');
  await expect(review.getByTestId('deck-updated')).toHaveText('1 h ago');
});

test('a person change on your own deck reads You, whatever name the server sends', async ({
  mount,
  page,
}) => {
  const [first] = decksLibrary.decks;
  const view: DecksLibraryView = {
    ...decksLibrary,
    decks: [
      {
        ...first,
        sharing: 'private',
        lastChange: { actor: 'Sam Rivera', actorKind: 'person', via: 'web', summary: 'created' },
      },
    ],
  };
  await stubApi(page, { '/api/decks': { json: view } });
  await mount(<DecksPage />, route);

  const row = page.getByTestId('deck-row');
  await expect(row.getByTestId('deck-last-change')).toHaveText('You · created');
  await expect(row.getByTestId('deck-sharing')).toHaveText('Private');
});

test('connect banner copies mcp url', async ({ mount, page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await stubApi(page, { '/api/decks': { json: decksLibrary } });
  await mount(<DecksPage />, route);

  const banner = page.getByTestId('connect-banner');
  await expect(banner).toContainText('Connect an agent:');
  await expect(banner.getByTestId('connect-mcp-url')).toHaveText('https://slideworks.example/mcp');
  await banner.getByTestId('connect-copy').click();
  await expect(banner.getByTestId('connect-copy')).toHaveText('Copied');
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(
    'https://slideworks.example/mcp',
  );
});

test('connect banner falls back to select and copy when the browser has no clipboard', async ({
  mount,
  page,
}) => {
  // Plain http on a host other than localhost: navigator.clipboard is undefined.
  await page.evaluate(() =>
    Object.defineProperty(navigator, 'clipboard', { value: undefined, configurable: true }),
  );
  await stubApi(page, { '/api/decks': { json: decksLibrary } });
  await mount(<DecksPage />, route);

  const banner = page.getByTestId('connect-banner');
  await banner.getByTestId('connect-copy').click();
  await expect(banner.getByTestId('connect-copy')).toHaveText('Select and copy');
  await expect
    .poll(() => page.evaluate(() => window.getSelection()?.toString()))
    .toBe('https://slideworks.example/mcp');
});

test('connect banner falls back to select and copy when the clipboard refuses', async ({
  mount,
  page,
}) => {
  await page.evaluate(() =>
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: () => Promise.reject(new DOMException('Denied', 'NotAllowedError')) },
      configurable: true,
    }),
  );
  await stubApi(page, { '/api/decks': { json: decksLibrary } });
  await mount(<DecksPage />, route);

  const banner = page.getByTestId('connect-banner');
  await banner.getByTestId('connect-copy').click();
  await expect(banner.getByTestId('connect-copy')).toHaveText('Select and copy');
});

test('connect banner hides once an agent has connected', async ({ mount, page }) => {
  const view: DecksLibraryView = {
    ...decksLibrary,
    connect: { ...decksLibrary.connect, show: false },
  };
  await stubApi(page, { '/api/decks': { json: view } });
  await mount(<DecksPage />, route);

  await expect(page.getByTestId('deck-row')).toHaveCount(2);
  await expect(page.getByTestId('connect-banner')).toHaveCount(0);
});

test('shared with you rows open the player', async ({ mount, page }) => {
  await stubApi(page, { '/api/decks': { json: decksLibrary } });
  await mount(
    <LocationProbe>
      <DecksPage />
    </LocationProbe>,
    route,
  );

  const shared = page.getByTestId('shared-with-me').getByTestId('shared-row');
  await expect(shared).toHaveCount(1);
  await expect(shared.getByTestId('shared-owner')).toHaveText('Alex Chen');
  await expect(shared.getByTestId('deck-last-change')).toHaveText('Cursor via MCP · updated notes');
  await expect(shared.getByTestId('deck-updated')).toHaveText('Yesterday');

  await shared.getByTestId('deck-title').click();
  await expect(page.getByTestId('location')).toHaveText('/d/Ob3nWk1Jn9Ef/onboarding-week-one');
});

test('new deck posts and navigates', async ({ mount, page }) => {
  const posted: unknown[] = [];
  await stubApi(page, { '/api/looks': { json: looks } });
  await page.route('**/api/decks', (request) => {
    if (request.request().method() === 'POST') {
      posted.push(request.request().postDataJSON());
      return request.fulfill({
        status: 201,
        json: { id: 'N3wDeck0Id12', slug: 'team-retro', playUrl: '/d/N3wDeck0Id12/team-retro' },
      });
    }
    return request.fulfill({ json: decksLibrary });
  });
  await mount(
    <LocationProbe>
      <DecksPage />
    </LocationProbe>,
    route,
  );

  await page.getByTestId('new-deck').click();
  await page.getByTestId('new-deck-title').fill('Team retro');
  await page.getByTestId('new-deck-create').click();

  await expect(page.getByTestId('location')).toHaveText('/decks/N3wDeck0Id12');
  expect(posted).toEqual([{ title: 'Team retro', template: 'talk', palette: 'cobalt' }]);
});

test('new deck shows the server error', async ({ mount, page }) => {
  await stubApi(page, { '/api/looks': { json: looks } });
  await page.route('**/api/decks', (request) =>
    request.request().method() === 'POST'
      ? request.fulfill({ status: 400, json: { error: 'Title is required.' } })
      : request.fulfill({ json: decksLibrary }),
  );
  await mount(<DecksPage />, route);

  await page.getByTestId('new-deck').click();
  await page.getByTestId('new-deck-title').fill('   ');
  await page.getByTestId('new-deck-create').click();

  await expect(page.getByTestId('new-deck-error')).toHaveText('Title is required.');
});

test('new deck cancel puts focus back on new deck', async ({ mount, page }) => {
  await stubApi(page, { '/api/decks': { json: decksLibrary }, '/api/looks': { json: looks } });
  await mount(<DecksPage />, route);

  await page.getByTestId('new-deck').click();
  await expect(page.getByTestId('new-deck-title')).toBeFocused();
  await page.getByTestId('new-deck-cancel').click();

  await expect(page.getByTestId('new-deck-form')).toHaveCount(0);
  await expect(page.getByTestId('new-deck')).toBeFocused();
});

test('a failed new deck puts focus back on the title', async ({ mount, page }) => {
  await stubApi(page, { '/api/looks': { json: looks } });
  await page.route('**/api/decks', (request) =>
    request.request().method() === 'POST'
      ? request.fulfill({ status: 400, json: { error: 'Title is required.' } })
      : request.fulfill({ json: decksLibrary }),
  );
  await mount(<DecksPage />, route);

  await page.getByTestId('new-deck').click();
  await page.getByTestId('new-deck-title').fill('   ');
  await page.getByTestId('new-deck-create').click();

  await expect(page.getByTestId('new-deck-error')).toHaveText('Title is required.');
  await expect(page.getByTestId('new-deck-title')).toBeFocused();
  await expect(page.getByTestId('new-deck-title')).toHaveAccessibleDescription(
    'Title is required.',
  );
});

test('the page has one h1, named tables and a labelled present column', async ({ mount, page }) => {
  await stubApi(page, { '/api/decks': { json: decksLibrary } });
  await mount(<DecksPage />, route);

  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Decks');
  const own = page.getByRole('table', { name: 'Your decks' });
  const shared = page.getByRole('table', { name: 'Shared with you' });
  await expect(own).toHaveCount(1);
  await expect(shared).toHaveCount(1);
  for (const table of [own, shared]) {
    await expect(table.getByRole('columnheader').last()).toHaveAccessibleName('Present');
    await expect(table.getByRole('columnheader').last().locator('span')).toHaveClass(/sr-only/);
  }
});

test('empty state', async ({ mount, page }) => {
  const view: DecksLibraryView = { ...decksLibrary, decks: [], sharedWithMe: [] };
  await stubApi(page, { '/api/decks': { json: view } });
  await mount(<DecksPage />, route);

  await expect(page.getByTestId('decks-empty')).toContainText('No decks yet');
  await expect(page.getByTestId('decks-count')).toHaveText('0');
  await expect(page.getByTestId('decks-table')).toHaveCount(0);
  await expect(page.getByTestId('shared-with-me')).toHaveCount(0);
  await expect(page.getByTestId('connect-banner')).toBeVisible();
  await expect(page.getByTestId('new-deck')).toBeVisible();
});

test('search filters rows', async ({ mount, page }) => {
  await stubApi(page, { '/api/decks': { json: decksLibrary } });
  await mount(<DecksPage />, route);
  await expect(page.getByTestId('deck-row')).toHaveCount(2);

  await page.getByTestId('decks-search').fill('q4 PLATFORM');
  await expect(page.getByTestId('deck-row')).toHaveCount(1);
  await expect(page.getByTestId('deck-row').getByTestId('deck-title')).toHaveText(
    'Q4 platform review',
  );
  await expect(page.getByTestId('shared-row')).toHaveCount(0);

  await page.getByTestId('decks-search').fill('onboarding');
  await expect(page.getByTestId('deck-row')).toHaveCount(0);
  await expect(page.getByTestId('shared-row')).toHaveCount(1);

  await page.getByTestId('decks-search').fill('');
  await expect(page.getByTestId('deck-row')).toHaveCount(2);
  await expect(page.getByTestId('shared-row')).toHaveCount(1);
});

test('api error shows message', async ({ mount, page }) => {
  await stubApi(page, { '/api/decks': { status: 500, json: { error: 'Something went wrong.' } } });
  await mount(<DecksPage />, route);

  await expect(page.getByTestId('decks-error')).toHaveText('Something went wrong.');
  await expect(page.getByTestId('decks-table')).toHaveCount(0);
  await expect(page.getByTestId('decks-empty')).toHaveCount(0);
});

test('api unreachable shows message', async ({ mount, page }) => {
  await stubApi(page, { '/api/decks': 'network-down' });
  await mount(<DecksPage />, route);

  await expect(page.getByTestId('decks-error')).toHaveText("Can't reach SlideWorks.");
});

// The baseline comes from CI's playwright job (never from a dev machine); E2
// refreshes the baselines from its report.
test('decks library screenshot', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1200, height: 640 });
  await stubApi(page, { '/api/decks': { json: decksLibrary } });
  const component = await mount(<DecksPage />, route);
  await expect(page.getByTestId('deck-row')).toHaveCount(2);
  await expect(component).toHaveScreenshot('decks-library.png');
});

test('opens the New deck dialog on the template the address names', async ({ mount, page }) => {
  const mine = { ...looks.templates[1], ref: 'tpl_Mine00000001', name: 'tpl_Mine00000001' };
  await stubApi(page, {
    '/api/decks': { json: decksLibrary },
    '/api/looks': { json: { ...looks, templates: [...looks.templates, mine] } },
  });
  await mount(
    <LocationProbe>
      <DecksPage />
    </LocationProbe>,
    { hooksConfig: { route: '/decks?template=tpl_Mine00000001' } },
  );

  await expect(page.getByTestId('new-deck-dialog')).toBeVisible();
  await expect(page.getByTestId('new-deck-template-tpl_Mine00000001')).toBeChecked();
  await expect(page.getByTestId('location')).toHaveText('/decks');
});

test('an address naming a template the person cannot pick opens on the default', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/decks': { json: decksLibrary }, '/api/looks': { json: looks } });
  await mount(<DecksPage />, { hooksConfig: { route: '/decks?template=tpl_Nobody000009' } });

  await expect(page.getByRole('radio', { name: /^Talk/ })).toBeChecked();
  await expect(page.getByTestId('new-deck-template-unavailable')).toHaveText(
    'That template is not available to you.',
  );
});
