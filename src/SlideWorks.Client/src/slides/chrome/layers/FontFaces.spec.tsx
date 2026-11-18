import { test, expect } from '@playwright/experimental-ct-react';
import type { LookFont } from '../../../api/look';
import type { LookView } from '../../../api/types';
import { lookStyle } from '../../../look/lookStyle';
import { LAYOUT_SLOTS } from '../../../test-support/fixtures/layoutSlots';
import { lookViewOf, uploadedLook } from '../../../test-support/fixtures/lookViewOf';
import { deckPlay } from '../../../test-support/fixtures/screens';
import { JoinProvider } from '../../JoinContext';
import { SlideRenderer } from '../../SlideRenderer';
import type { SlideDeck } from '../../slots';

test.use({ viewport: { width: 1920, height: 1080 } });

const FONT_FILE = 'node_modules/@fontsource/inter/files/inter-latin-400-normal.woff2';
const slide = {
  number: 2,
  layout: 'bullets' as const,
  part: null,
  deepDive: false,
  slots: LAYOUT_SLOTS.bullets,
};
const fonts: LookFont[] = [
  {
    family: 'Harbour Head',
    role: 'head',
    url: '/api/templates/tpl_h/assets/head',
    weight: 800,
    style: 'normal',
  },
  {
    family: 'Harbour Body',
    role: 'body',
    url: '/api/templates/tpl_h/assets/body',
    weight: 400,
    style: 'italic',
  },
];

function frames(look: LookView, count: number) {
  const deck: SlideDeck = { title: 'Ship smaller', author: null, parts: [], look, deepDives: [] };
  return (
    <div style={lookStyle(look)}>
      <JoinProvider deckId={deckPlay.id} join={{ ...deckPlay.join, live: false }} owner={false}>
        {Array.from({ length: count }, (_, i) => (
          <SlideRenderer key={i} slide={slide} deck={deck} total={4} step={0} />
        ))}
      </JoinProvider>
    </div>
  );
}

const lookWith = (list: LookFont[]) => ({ ...uploadedLook, fonts: list });

// The faces on the page, as family|weight|style.
const facesOf = (page: import('playwright/test').Page) =>
  page.evaluate(() =>
    Array.from(document.fonts)
      .map((face) => `${face.family}|${face.weight}|${face.style}`)
      .filter((face) => face.includes('Harbour')),
  );

test('adds one face per file', async ({ mount, page }) => {
  await page.route('**/api/templates/tpl_h/assets/*', (route) =>
    route.fulfill({ path: FONT_FILE, contentType: 'font/woff2' }),
  );

  await mount(frames(lookWith(fonts), 1));

  await expect
    .poll(() => facesOf(page))
    .toEqual(expect.arrayContaining(['"Harbour Head"|800|normal', '"Harbour Body"|400|italic']));
  expect(await facesOf(page)).toHaveLength(2);
});

test('adds a file once when two frames mount', async ({ mount, page }) => {
  const requested: string[] = [];
  await page.route('**/api/templates/tpl_h/assets/*', (route) => {
    requested.push(new URL(route.request().url()).pathname);
    return route.fulfill({ path: FONT_FILE, contentType: 'font/woff2' });
  });

  await mount(frames(lookWith(fonts), 2));

  await expect.poll(() => requested.length).toBeGreaterThanOrEqual(2);
  await expect(page.getByTestId('slide-2')).toHaveCount(2);
  expect(await facesOf(page)).toHaveLength(2);
  expect([...requested].sort()).toEqual([
    '/api/templates/tpl_h/assets/body',
    '/api/templates/tpl_h/assets/head',
  ]);
});

test('adds nothing for a built-in look', async ({ mount, page }) => {
  const before = await page.evaluate(() => document.fonts.size);

  await mount(frames(lookViewOf('talk', 'cobalt'), 1));

  await expect(page.getByTestId('slide-2')).toHaveCount(1);
  expect(await page.evaluate(() => document.fonts.size)).toBe(before);
});

test('keeps drawing when a file fails to load', async ({ mount, page }) => {
  await page.route('**/api/templates/tpl_h/assets/*', (route) =>
    route.fulfill({ status: 404, json: { error: 'File not found' } }),
  );

  await mount(frames(lookWith(fonts), 1));

  await expect.poll(() => facesOf(page)).toEqual([]);
  await expect(page.getByTestId('slide-2')).toBeVisible();
  await expect(page.getByTestId('slide-2').getByRole('heading').first()).toBeVisible();
});
