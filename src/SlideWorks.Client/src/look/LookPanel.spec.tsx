import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { DeckPageLook, DeckPageView, LookChangeRequest, LooksView } from '../api/types';
import {
  PALETTES,
  TEMPLATES,
  lockingTemplate,
  lookViewOf,
  looksPaletteOf,
  looksTemplateOf,
} from '../test-support/fixtures/lookViewOf';
import { deckPage, lookChangeResult } from '../test-support/fixtures/screens';
import { LookPanel } from './LookPanel';

const lookPath = `**/api/decks/${deckPage.id}/look`;

// The built-in catalogue as GET /api/looks lists it, plus the locking template.
const catalogue: LooksView = {
  templates: [...TEMPLATES, lockingTemplate].map(looksTemplateOf),
  palettes: PALETTES.map(looksPaletteOf),
};

const passing = { pass: true, lowest: { pair: 'on-accent', ratio: 5.2, bar: 4.5 } } as const;

function deckOn(template: string | typeof lockingTemplate, palette: string): DeckPageView {
  const look: DeckPageLook = {
    ...lookViewOf(template, palette),
    chromeOverrides: {},
    contrast: passing,
  };
  return { ...deckPage, look };
}

type Answer = { status: number; json: unknown };

// Serves the catalogue and records each PUT body, answering it with `answer`.
async function stubLook(page: Page, answer: Answer = { status: 200, json: lookChangeResult }) {
  await page.route('**/api/looks', (route) => route.fulfill({ json: catalogue }));
  const puts: LookChangeRequest[] = [];
  await page.route(lookPath, (route) => {
    puts.push(route.request().postDataJSON() as LookChangeRequest);
    return route.fulfill(answer);
  });
  return puts;
}

test('each control sends its own PUT body and hands the saved look to onSaved', async ({
  mount,
  page,
}) => {
  const puts = await stubLook(page);
  const saved: string[] = [];
  const panel = await mount(
    <LookPanel deck={deckOn('talk', 'cobalt')} onSaved={(look) => saved.push(look.palette.name)} />,
  );

  const template = panel.getByTestId('look-template');
  await expect(template.locator('option')).toHaveCount(TEMPLATES.length + 1);
  await template.selectOption('workshop');
  await expect.poll(() => puts.length).toBe(1);
  await panel.getByTestId('look-palette').selectOption('ember');
  await expect.poll(() => puts.length).toBe(2);
  await panel.getByTestId('look-tracker').click();
  await expect.poll(() => puts.length).toBe(3);
  await panel.getByTestId('look-progress').click();
  await expect.poll(() => puts.length).toBe(4);
  await panel.getByTestId('look-footer').selectOption('off');
  await expect.poll(() => puts.length).toBe(5);
  await panel.getByTestId('look-draft').click();
  await expect.poll(() => puts.length).toBe(6);

  const chrome = lookViewOf('talk', 'cobalt').template.chrome;
  expect(puts).toEqual([
    { template: 'workshop' },
    { palette: 'ember' },
    { chrome: { tracker: !chrome.tracker } },
    { chrome: { progress: !chrome.progress } },
    { chrome: { footer: 'off' } },
    { draft: !chrome.draft },
  ]);
  await expect.poll(() => saved.length).toBe(6);
  expect(saved[0]).toBe(lookChangeResult.look.palette.name);
});

test('a template switch shows the warnings it returns', async ({ mount, page }) => {
  await stubLook(page);
  const panel = await mount(<LookPanel deck={deckOn('talk', 'cobalt')} onSaved={() => {}} />);

  await panel.getByTestId('look-template').selectOption('workshop');
  await expect(panel.getByTestId('look-warning')).toHaveText(lookChangeResult.warnings);
  await expect(panel.getByTestId('look-error')).toHaveCount(0);
});

test('a 400 shows its error text', async ({ mount, page }) => {
  const error = "Template 'locked-talk' locks its palette";
  await stubLook(page, { status: 400, json: { error } });
  const panel = await mount(<LookPanel deck={deckOn('talk', 'cobalt')} onSaved={() => {}} />);

  await panel.getByTestId('look-palette').selectOption('plum');
  await expect(panel.getByTestId('look-error')).toHaveText(error);
  await expect(panel.getByTestId('look-warnings')).toHaveCount(0);
});

test('a template that locks its palette disables the palette select', async ({ mount, page }) => {
  await stubLook(page);
  const panel = await mount(
    <LookPanel deck={deckOn(lockingTemplate, 'ember')} onSaved={() => {}} />,
  );

  await expect(panel.getByTestId('look-template')).toBeEnabled();
  await expect(panel.getByTestId('look-palette')).toBeDisabled();
  await expect(panel.getByTestId('look-palette-locked')).toHaveText(
    `The ${lockingTemplate.label} template sets the palette.`,
  );
});

test('the swatch bar and the contrast message show the deck palette', async ({ mount, page }) => {
  await stubLook(page);
  const failing = {
    ...deckOn('talk', 'cobalt'),
    look: {
      ...deckOn('talk', 'cobalt').look,
      contrast: { pass: false, lowest: { pair: 'panel-accent', ratio: 3.1, bar: 4.5 } },
    },
  } satisfies DeckPageView;
  const panel = await mount(<LookPanel deck={deckOn('talk', 'cobalt')} onSaved={() => {}} />);

  await expect(panel.getByTestId('look-swatch').locator('span')).toHaveCount(
    PALETTES.find((palette) => palette.name === 'cobalt')!.swatch.length,
  );
  await expect(panel.getByTestId('look-contrast')).toHaveText(
    'All pairs pass. Closest: on-accent at 5.2:1 (needs 4.5:1).',
  );

  await panel.update(<LookPanel deck={failing} onSaved={() => {}} />);
  await expect(panel.getByTestId('look-contrast')).toHaveText(
    'panel-accent is 3.1:1, below the 4.5:1 it needs.',
  );
  await expect(panel.getByTestId('look-contrast')).toHaveAttribute('data-pass', 'false');
});

test('the deep-dive list comes from the slides', async ({ mount, page }) => {
  await stubLook(page);
  const deck = deckOn('talk', 'cobalt');
  const panel = await mount(<LookPanel deck={deck} onSaved={() => {}} />);

  const flagged = deck.slides.filter((slide) => slide.deepDive).map((slide) => slide.number);
  expect(flagged.length).toBeGreaterThan(0);
  await expect(panel.getByTestId('look-deep-dives')).toHaveText(
    `${flagged.length === 1 ? 'Slide' : 'Slides'} ${flagged.join(', ')}`,
  );

  const none = { ...deck, slides: deck.slides.map((slide) => ({ ...slide, deepDive: false })) };
  await panel.update(<LookPanel deck={none} onSaved={() => {}} />);
  await expect(panel.getByTestId('look-deep-dives')).toHaveText('None');
});

test('the share card URL changes with the revision and the sharing', async ({ mount, page }) => {
  await stubLook(page);
  await page.route(`**/api/cards/${deckPage.id}.png*`, (route) =>
    route.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
  const deck = deckOn('talk', 'cobalt');
  const panel = await mount(<LookPanel deck={deck} onSaved={() => {}} />);

  const card = panel.getByTestId('look-card');
  await expect(card).toHaveAttribute(
    'src',
    `/api/cards/${deck.id}.png?v=${deck.revision}-${deck.sharing.level}`,
  );

  await panel.update(
    <LookPanel deck={{ ...deck, revision: deck.revision + 1 }} onSaved={() => {}} />,
  );
  await expect(card).toHaveAttribute(
    'src',
    `/api/cards/${deck.id}.png?v=${deck.revision + 1}-${deck.sharing.level}`,
  );

  await panel.update(
    <LookPanel
      deck={{ ...deck, revision: deck.revision + 1, sharing: { level: 'private', viewerCount: 0 } }}
      onSaved={() => {}}
    />,
  );
  await expect(card).toHaveAttribute(
    'src',
    `/api/cards/${deck.id}.png?v=${deck.revision + 1}-private`,
  );
});

// A deck on version 2 of an uploaded template whose latest published version is 3.
const uploaded = {
  ...looksTemplateOf(TEMPLATES[0]),
  label: 'Northwind',
  ref: 'tpl_Zk3fWy8Jn5Ts',
  name: 'tpl_Zk3fWy8Jn5Ts',
  version: 3,
  builtIn: false,
};

function deckBehind(overrides: Partial<DeckPageView> = {}, latest = 3): DeckPageView {
  const base = deckOn('talk', 'cobalt');
  const look: DeckPageLook = {
    ...base.look,
    template: { ...base.look.template, name: uploaded.ref, label: uploaded.label },
    templateRef: uploaded.ref,
    templateVersion: 2,
    latestVersion: latest,
  };
  return { ...base, look, ...overrides };
}

const updated: DeckPageLook = { ...deckBehind().look, templateVersion: 3 };

async function stubUpdate(page: Page, answer: Answer) {
  await page.route('**/api/looks', (route) =>
    route.fulfill({ json: { ...catalogue, templates: [...catalogue.templates, uploaded] } }),
  );
  const puts: unknown[] = [];
  await page.route(lookPath, (route) => {
    puts.push(route.request().postDataJSON());
    return route.fulfill(answer);
  });
  return puts;
}

test('offers the newer version and applies it', async ({ mount, page }) => {
  const puts = await stubUpdate(page, { status: 200, json: { look: updated, warnings: [] } });
  const saved: number[] = [];
  const panel = await mount(
    <LookPanel deck={deckBehind()} onSaved={(look) => saved.push(look.templateVersion ?? 0)} />,
  );

  const offer = panel.getByTestId('look-version-offer');
  await expect(offer).toContainText('Northwind · version 2');
  await expect(offer).toContainText('version 3 available');
  const button = panel.getByTestId('look-version-update');
  await expect(button).toHaveAccessibleName('Update this deck to version 3 of Northwind');
  await button.click();

  await expect(panel.getByTestId('look-version-message')).toHaveText('Now on version 3.');
  await expect(panel.getByTestId('look-version-message')).toBeFocused();
  await expect(panel.getByTestId('look-version-message')).toHaveAttribute('role', 'status');
  expect(puts).toEqual([{ templateVersion: 'latest' }]);
  expect(saved).toEqual([3]);
  await expect(offer).toHaveCount(0);
});

test('disables the button and says Updating while it runs', async ({ mount, page }) => {
  let release = () => {};
  await page.route('**/api/looks', (route) => route.fulfill({ json: catalogue }));
  await page.route(
    lookPath,
    (route) =>
      new Promise<void>((resolve) => {
        release = () => resolve(route.fulfill({ json: { look: updated, warnings: [] } }));
      }),
  );
  const panel = await mount(<LookPanel deck={deckBehind()} onSaved={() => {}} />);

  await panel.getByTestId('look-version-update').click();
  await expect(panel.getByTestId('look-version-update')).toBeDisabled();
  await expect(panel.getByTestId('look-version-update')).toHaveText('Updating…');
  release();
  await expect(panel.getByTestId('look-version-message')).toBeVisible();
});

test('reports a failed update', async ({ mount, page }) => {
  await stubUpdate(page, {
    status: 400,
    json: { error: 'template_refused', message: 'That version is not published.' },
  });
  const panel = await mount(<LookPanel deck={deckBehind()} onSaved={() => {}} />);

  await panel.getByTestId('look-version-update').click();

  await expect(panel.getByTestId('look-version-error')).toHaveText(
    'That version is not published.',
  );
  await expect(panel.getByTestId('look-version-error')).toHaveAttribute('role', 'alert');
  await expect(panel.getByTestId('look-version-offer')).toBeVisible();
  await expect(panel.getByTestId('look-version-update')).toBeFocused();
  await expect(panel.getByTestId('look-version-update')).toBeEnabled();
});

test('a failed update with no sentence says to try again', async ({ mount, page }) => {
  await stubUpdate(page, { status: 500, json: {} });
  const panel = await mount(<LookPanel deck={deckBehind()} onSaved={() => {}} />);

  await panel.getByTestId('look-version-update').click();

  await expect(panel.getByTestId('look-version-error')).toHaveText(
    'Could not update this deck. Try again.',
  );
});

test('makes no offer on the latest version', async ({ mount, page }) => {
  await stubUpdate(page, { status: 200, json: lookChangeResult });
  const panel = await mount(<LookPanel deck={deckBehind({}, 2)} onSaved={() => {}} />);

  await expect(panel.getByTestId('look-template')).toHaveValue(uploaded.ref);
  await expect(panel.getByTestId('look-version-offer')).toHaveCount(0);
});

test('makes no offer on a built-in template', async ({ mount, page }) => {
  await stubUpdate(page, { status: 200, json: lookChangeResult });
  const panel = await mount(<LookPanel deck={deckOn('talk', 'cobalt')} onSaved={() => {}} />);

  await expect(panel.getByTestId('look-template')).toBeVisible();
  await expect(panel.getByTestId('look-version-offer')).toHaveCount(0);
});

test('makes no offer to someone who is not the owner', async ({ mount, page }) => {
  await stubUpdate(page, { status: 200, json: lookChangeResult });
  const panel = await mount(<LookPanel deck={deckBehind({ isOwner: false })} onSaved={() => {}} />);

  await expect(panel.getByTestId('look-template')).toBeVisible();
  await expect(panel.getByTestId('look-version-offer')).toHaveCount(0);
});

test('shows the layout warnings after an update', async ({ mount, page }) => {
  await stubUpdate(page, {
    status: 200,
    json: {
      look: updated,
      warnings: ["Slide 7: layout 'gantt' is not in the 'Northwind' template"],
    },
  });
  const panel = await mount(<LookPanel deck={deckBehind()} onSaved={() => {}} />);

  await panel.getByTestId('look-version-update').click();

  await expect(panel.getByTestId('look-warning')).toHaveText([
    "Slide 7: layout 'gantt' is not in the 'Northwind' template",
  ]);
});

test('draws a template name as text, never markup', async ({ mount, page }) => {
  await stubUpdate(page, { status: 200, json: lookChangeResult });
  const deck = deckBehind();
  const hostile: DeckPageView = {
    ...deck,
    look: { ...deck.look, template: { ...deck.look.template, label: '<b>Bold</b>' } },
  };
  const panel = await mount(<LookPanel deck={hostile} onSaved={() => {}} />);

  await expect(panel.getByTestId('look-version-offer')).toContainText('<b>Bold</b> · version 2');
  await expect(panel.getByTestId('look-version-offer').locator('b')).toHaveCount(0);
});

test('screenshot: look panel with a version offer', async ({ mount, page }) => {
  await page.setViewportSize({ width: 480, height: 900 });
  await stubUpdate(page, { status: 200, json: lookChangeResult });
  await page.route(`**/api/cards/${deckPage.id}.png*`, (route) =>
    route.fulfill({ path: 'src/test-support/fixtures/sample-image.png', contentType: 'image/png' }),
  );
  const panel = await mount(<LookPanel deck={deckBehind()} onSaved={() => {}} />);
  await expect(panel.getByTestId('look-version-offer')).toBeVisible();
  await expect
    .poll(() =>
      panel.getByTestId('look-card').evaluate((img: HTMLImageElement) => img.naturalWidth),
    )
    .toBeGreaterThan(0);
  await expect(panel).toHaveScreenshot('look-panel-version-offer.png');
});
