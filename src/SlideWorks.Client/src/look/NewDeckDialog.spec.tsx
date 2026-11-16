import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { CreateDeckResult, LooksView } from '../api/types';
import {
  PALETTES,
  TEMPLATES,
  lockingTemplate,
  looksPaletteOf,
  looksTemplateOf,
} from '../test-support/fixtures/lookViewOf';
import { stubApi } from '../test-support/stubApi';
import { NewDeckDialog } from './NewDeckDialog';

// The built-in catalogue, plus a template that locks its palette (none of the
// built-ins does).
const looks: LooksView = {
  templates: [...TEMPLATES, lockingTemplate].map(looksTemplateOf),
  palettes: PALETTES.map(looksPaletteOf),
};

const created: CreateDeckResult = {
  id: 'N3wDeck0Id12',
  slug: 'ship-smaller',
  playUrl: '/d/N3wDeck0Id12/ship-smaller',
};

const accentOfPreview = (page: Page) =>
  page
    .getByTestId('look-preview-slide')
    .first()
    .locator('[data-testid^="slide-"]')
    .first()
    .evaluate((el) => getComputedStyle(el).getPropertyValue('--sw-accent').trim());

const noop = () => {};

test('the defaults are the catalogue default template and palette', async ({ mount, page }) => {
  await stubApi(page, { '/api/looks': { json: looks } });
  await mount(<NewDeckDialog open onClose={noop} onCreated={noop} />);

  const dialog = page.getByTestId('new-deck-dialog');
  await expect(dialog).toHaveAccessibleName('New deck');
  await expect(dialog.getByRole('radio', { name: /^Talk/ })).toBeChecked();
  await expect(page.getByTestId('new-deck-palette')).toHaveValue('cobalt');
  await expect(page.getByTestId('new-deck-palette')).toBeEnabled();
  await expect(page.getByTestId('look-preview')).toHaveAttribute('data-template', 'talk');
  await expect(page.getByTestId('look-preview')).toHaveAttribute('data-palette', 'cobalt');
  await expect(page.getByTestId('look-preview-slide')).toHaveCount(2);
  await expect(page.getByTestId('new-deck-title')).toBeFocused();
});

test('changing the palette re-skins the preview', async ({ mount, page }) => {
  await stubApi(page, { '/api/looks': { json: looks } });
  await mount(<NewDeckDialog open onClose={noop} onCreated={noop} />);

  await expect(page.getByTestId('look-preview')).toHaveAttribute('data-palette', 'cobalt');
  const cobaltAccent = await accentOfPreview(page);
  await page.getByTestId('new-deck-palette').selectOption('plum');

  await expect(page.getByTestId('look-preview')).toHaveAttribute('data-palette', 'plum');
  const plumAccent = await accentOfPreview(page);
  expect(cobaltAccent).toBe(looks.palettes.find((p) => p.name === 'cobalt')?.tokens.accent);
  expect(plumAccent).toBe(looks.palettes.find((p) => p.name === 'plum')?.tokens.accent);
  expect(plumAccent).not.toBe(cobaltAccent);
});

test('a template that locks its palette greys the palette out', async ({ mount, page }) => {
  await stubApi(page, { '/api/looks': { json: looks } });
  await mount(<NewDeckDialog open onClose={noop} onCreated={noop} />);

  await page.getByRole('radio', { name: new RegExp(`^${lockingTemplate.label}`) }).check();

  await expect(page.getByTestId('new-deck-palette')).toBeDisabled();
  await expect(page.getByTestId('new-deck-palette')).toHaveValue(lockingTemplate.palette ?? '');
  await expect(page.getByTestId('new-deck-palette-note')).toHaveText(
    `The ${lockingTemplate.label} template locks its palette.`,
  );
  await expect(page.getByTestId('look-preview')).toHaveAttribute(
    'data-palette',
    lockingTemplate.palette ?? '',
  );

  await page.getByRole('radio', { name: /^Workshop/ }).check();
  await expect(page.getByTestId('new-deck-palette')).toBeEnabled();
});

test('create posts the title, template and palette and hands back the deck', async ({
  mount,
  page,
}) => {
  const posted: unknown[] = [];
  await stubApi(page, { '/api/looks': { json: looks } });
  await page.route('**/api/decks', (route) => {
    posted.push(route.request().postDataJSON());
    return route.fulfill({ status: 201, json: created });
  });
  const results: CreateDeckResult[] = [];
  await mount(<NewDeckDialog open onClose={noop} onCreated={(result) => results.push(result)} />);

  await page.getByTestId('new-deck-title').fill('Ship smaller');
  await page.getByRole('radio', { name: /^Update/ }).check();
  await page.getByTestId('new-deck-palette').selectOption('plum');
  await page.getByTestId('new-deck-create').click();

  await expect.poll(() => results).toEqual([created]);
  expect(posted).toEqual([{ title: 'Ship smaller', template: 'update', palette: 'plum' }]);
});

test('a 400 shows its error and keeps the dialog open', async ({ mount, page }) => {
  await stubApi(page, {
    '/api/looks': { json: looks },
    '/api/decks': { status: 400, json: { error: "Unknown palette 'plum'" } },
  });
  const results: CreateDeckResult[] = [];
  await mount(<NewDeckDialog open onClose={noop} onCreated={(result) => results.push(result)} />);

  await page.getByTestId('new-deck-title').fill('Ship smaller');
  await page.getByTestId('new-deck-create').click();

  await expect(page.getByTestId('new-deck-error')).toHaveText("Unknown palette 'plum'");
  await expect(page.getByTestId('new-deck-dialog')).toBeVisible();
  await expect(page.getByTestId('new-deck-title')).toBeFocused();
  await expect(page.getByTestId('new-deck-create')).toBeEnabled();
  expect(results).toEqual([]);
});

test('cancel and Escape close it', async ({ mount, page }) => {
  await stubApi(page, { '/api/looks': { json: looks } });
  let closed = 0;
  await mount(<NewDeckDialog open onClose={() => (closed += 1)} onCreated={noop} />);

  await page.getByTestId('new-deck-cancel').click();
  await expect.poll(() => closed).toBe(1);
  await page.getByTestId('new-deck-title').press('Escape');
  await expect.poll(() => closed).toBe(2);
});

test('closed, it renders nothing and loads nothing', async ({ mount, page }) => {
  const requests: string[] = [];
  page.on('request', (request) => {
    if (request.url().includes('/api/')) requests.push(request.url());
  });
  await mount(<NewDeckDialog open={false} onClose={noop} onCreated={noop} />);

  await expect(page.getByTestId('new-deck-dialog')).toHaveCount(0);
  expect(requests).toEqual([]);
});
