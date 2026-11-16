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
