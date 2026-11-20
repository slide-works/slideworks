import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { CreateDeckResult, LooksTemplate, LooksView } from '../api/types';
import {
  PALETTES,
  TEMPLATES,
  lockingTemplate,
  looksPaletteOf,
  looksTemplateOf,
} from '../test-support/fixtures/lookViewOf';
import { looks as looksSample } from '../test-support/fixtures/screens';
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

test('Tab and Shift+Tab wrap inside the dialog and never reach the page behind', async ({
  mount,
  page,
}) => {
  await stubApi(page, { '/api/looks': { json: looks } });
  await mount(
    <div>
      <button type="button" data-testid="behind">
        Behind
      </button>
      <NewDeckDialog open onClose={noop} onCreated={noop} />
    </div>,
  );

  await expect(page.getByTestId('new-deck-create')).toBeEnabled();
  await page.getByTestId('new-deck-create').focus();
  await page.keyboard.press('Tab');
  await expect(page.getByTestId('new-deck-title')).toBeFocused();
  await page.keyboard.press('Shift+Tab');
  await expect(page.getByTestId('new-deck-create')).toBeFocused();

  // A full lap of tab stops stays inside the dialog.
  for (let stop = 0; stop < 8; stop += 1) {
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('behind')).not.toBeFocused();
    expect(
      await page.evaluate(() =>
        Boolean(document.activeElement?.closest('[data-testid="new-deck-dialog"]')),
      ),
    ).toBe(true);
  }
});

test('Escape closes it wherever focus is inside it', async ({ mount, page }) => {
  await stubApi(page, { '/api/looks': { json: looks } });
  let closed = 0;
  await mount(<NewDeckDialog open onClose={() => (closed += 1)} onCreated={noop} />);

  await page.getByTestId('new-deck-palette').focus();
  await page.keyboard.press('Escape');
  await expect.poll(() => closed).toBe(1);

  // Focus on nothing in particular: a click on the heading text.
  await page.getByRole('heading', { name: 'New deck' }).click();
  await page.keyboard.press('Escape');
  await expect.poll(() => closed).toBe(2);
});

test('while the catalogue loads it says so and Create waits', async ({ mount, page }) => {
  await page.route('**/api/looks', () => {
    // Never answered: the catalogue stays loading.
  });
  await mount(<NewDeckDialog open onClose={noop} onCreated={noop} />);

  await expect(page.getByTestId('new-deck-looks-loading')).toHaveText(
    'Loading templates and palettes…',
  );
  await expect(page.getByTestId('new-deck-looks-loading')).toHaveRole('status');
  await expect(page.getByTestId('new-deck-create')).toBeDisabled();
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

// Uploaded templates as GET /api/looks lists them: one of the caller's own, one shared with them, and one that
// locks its palette (the contract sample).
const sample = looksSample.templates[0];
const mine: LooksTemplate = {
  ...sample,
  label: 'Studio house style',
  purpose: 'Our own talks.',
  lockedPalette: null,
  ref: 'tpl_Mine00000001',
  name: 'tpl_Mine00000001',
  version: 3,
  owner: null,
};
const shared: LooksTemplate = {
  ...mine,
  label: 'Northwind',
  ref: 'tpl_Shared000002',
  name: 'tpl_Shared000002',
  owner: 'Mira Okafor',
};
const uploadedLocked: LooksTemplate = {
  ...shared,
  label: 'Harbour',
  ref: 'tpl_Locked000003',
  name: 'tpl_Locked000003',
  lockedPalette: 'ember',
};
const withUploads: LooksView = {
  templates: [
    ...looks.templates.slice(0, 2),
    shared,
    uploadedLocked,
    ...looks.templates.slice(2),
    mine,
  ],
  palettes: looks.palettes,
};

test('lists my templates above the built-ins', async ({ mount, page }) => {
  await stubApi(page, { '/api/looks': { json: withUploads } });
  await mount(<NewDeckDialog open onClose={noop} onCreated={noop} />);

  const rows = page.getByTestId('new-deck-template');
  await expect(rows.first()).toHaveAttribute('data-template', mine.ref);
  await expect(rows.first()).toContainText('yours');
  await expect(rows.first()).toContainText('Our own talks.');
  await expect(page.getByTestId(`new-deck-template-${mine.ref}`)).toBeVisible();
  await expect(rows.nth(1)).toHaveAttribute('data-template', shared.ref);
  await expect(rows.nth(3)).toHaveAttribute('data-template', 'talk');
  await expect(page.getByRole('radio', { name: /^Talk/ })).toBeChecked();
});

test('marks a shared template with its owner', async ({ mount, page }) => {
  await stubApi(page, { '/api/looks': { json: withUploads } });
  await mount(<NewDeckDialog open onClose={noop} onCreated={noop} />);

  const row = page.getByTestId('new-deck-template').filter({ hasText: 'Northwind' });
  await expect(row).toContainText('shared by Mira Okafor');
  await expect(row).not.toContainText('yours');
});

test('renders a template name as text, never markup', async ({ mount, page }) => {
  const hostile = { ...mine, label: '<img src=x onerror=alert(1)>' };
  await stubApi(page, {
    '/api/looks': { json: { ...withUploads, templates: [...looks.templates, hostile] } },
  });
  await mount(<NewDeckDialog open onClose={noop} onCreated={noop} />);

  await expect(page.getByTestId('new-deck-template').first()).toContainText('<img src=x');
  await expect(page.getByTestId('new-deck-template').locator('img')).toHaveCount(0);
});

test('locks the palette a template sets', async ({ mount, page }) => {
  await stubApi(page, { '/api/looks': { json: withUploads } });
  await mount(<NewDeckDialog open onClose={noop} onCreated={noop} />);

  await page.getByTestId(`new-deck-template-${uploadedLocked.ref}`).check();

  const palette = page.getByTestId('new-deck-palette');
  await expect(palette).toBeDisabled();
  await expect(palette).toHaveValue('ember');
  await expect(palette.locator('option:checked')).toHaveText('Ember (set by the template)');
  await expect(page.getByTestId('look-preview')).toHaveAttribute('data-palette', 'ember');

  await page.getByTestId(`new-deck-template-${mine.ref}`).check();
  await expect(palette).toBeEnabled();
});

test('creates a deck on an uploaded template', async ({ mount, page }) => {
  const posted: unknown[] = [];
  await stubApi(page, { '/api/looks': { json: withUploads } });
  await page.route('**/api/decks', (route) => {
    posted.push(route.request().postDataJSON());
    return route.fulfill({ status: 201, json: created });
  });
  const results: CreateDeckResult[] = [];
  await mount(<NewDeckDialog open onClose={noop} onCreated={(result) => results.push(result)} />);

  await page.getByTestId('new-deck-title').fill('Ship smaller');
  await page.getByTestId(`new-deck-template-${mine.ref}`).check();
  await page.getByTestId('new-deck-create').click();

  await expect.poll(() => results).toEqual([created]);
  expect(posted).toEqual([{ title: 'Ship smaller', template: mine.ref, palette: 'cobalt' }]);
});

test('a template that sets its own palette is created with the reserved name', async ({
  mount,
  page,
}) => {
  const own = { ...uploadedLocked, lockedPalette: 'template' };
  const posted: unknown[] = [];
  await stubApi(page, {
    '/api/looks': {
      json: {
        ...withUploads,
        templates: [...withUploads.templates.filter((t) => t.ref !== own.ref), own],
      },
    },
  });
  await page.route('**/api/decks', (route) => {
    posted.push(route.request().postDataJSON());
    return route.fulfill({ status: 201, json: created });
  });
  await mount(<NewDeckDialog open onClose={noop} onCreated={noop} />);

  await page.getByTestId('new-deck-title').fill('Own');
  await page.getByTestId('new-deck-template-tpl_Locked000003').check();
  await expect(page.getByTestId('new-deck-palette')).toBeDisabled();
  await page.getByTestId('new-deck-create').click();

  await expect.poll(() => posted.length).toBe(1);
  expect(posted).toEqual([{ title: 'Own', template: own.ref, palette: 'template' }]);
});

test('starts on the template it is given', async ({ mount, page }) => {
  await stubApi(page, { '/api/looks': { json: withUploads } });
  await mount(<NewDeckDialog open onClose={noop} onCreated={noop} initialTemplate={shared.ref} />);

  await expect(page.getByTestId(`new-deck-template-${shared.ref}`)).toBeChecked();
  await expect(page.getByTestId('new-deck-template-unavailable')).toHaveCount(0);
});

test('a template it cannot pick opens on the default with a note', async ({ mount, page }) => {
  await stubApi(page, { '/api/looks': { json: withUploads } });
  await mount(
    <NewDeckDialog open onClose={noop} onCreated={noop} initialTemplate="tpl_Nobody000009" />,
  );

  await expect(page.getByRole('radio', { name: /^Talk/ })).toBeChecked();
  await expect(page.getByTestId('new-deck-template-unavailable')).toHaveText(
    'That template is not available to you.',
  );
});

// The baseline comes from CI's playwright job (never from a dev machine).
test('screenshot: new deck dialog with my templates', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1100, height: 900 });
  await stubApi(page, { '/api/looks': { json: withUploads } });
  await mount(<NewDeckDialog open onClose={noop} onCreated={noop} />);
  await expect(page.getByTestId('look-preview')).toBeVisible();
  await expect(page.getByTestId('new-deck-dialog')).toHaveScreenshot('new-deck-my-templates.png');
});
