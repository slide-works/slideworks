import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { TwoColumnSlots } from '../slots/two-column';
import type { SlideDeck } from '../slots';

// The two-column layout's SW-50 additions: a callout under the columns, icons
// in both columns, emphasis in the headline, a right column that can arrive on
// a step and titles of 60 characters.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function twoColumn(slots: TwoColumnSlots, number = 3): RenderableSlide {
  return { number, layout: 'two-column', part: null, deepDive: false, surface: undefined, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const WIDE_TEXT =
  'Many Women Who Wander Wide Meadows Mow Moss With Me When Warm, ' +
  'Whom We Name WOMBAT MUMMY Mavens, Wow, Momentum Wins Most Wednesdays. ';

// Wide-letter text (capitals, W and M) of exactly `length` characters.
function wide(length: number): string {
  let text = '';
  while (text.length < length) text += WIDE_TEXT;
  return text.slice(0, length).trimEnd().padEnd(length, 'M');
}

const NOT_TEXT = new Set(['icon', 'tone']);

function widen(value: unknown, key?: string): unknown {
  if (typeof value === 'string')
    return key !== undefined && NOT_TEXT.has(key) ? value : wide(value.length);
  if (Array.isArray(value)) return value.map((item) => widen(item));
  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, widen(v, k)]));
  }
  return value;
}

const base: TwoColumnSlots = {
  headline: 'Two columns',
  left: { title: 'Left', items: ['One', 'Two'] },
  right: { title: 'Right', text: 'A paragraph that fills the right column.' },
};

const box = (page: Page, testId: string) => page.getByTestId(testId).boundingBox();

test('right column hidden keeps its space', async ({ mount, page }) => {
  const slots: TwoColumnSlots = { ...base, reveal: 'one-by-one' };
  const component = await mount(render(twoColumn(slots), 0));
  const wrapper = page.getByTestId('two-column-right').locator('xpath=..');
  await expect(wrapper).toHaveAttribute('data-revealed', 'false');
  const hidden = (await box(page, 'two-column-right'))!;
  await component.update(render(twoColumn(slots), 1));
  await expect(wrapper).toHaveAttribute('data-revealed', 'true');
  const shown = (await box(page, 'two-column-right'))!;
  expect(hidden).toEqual(shown);
  expect(hidden.width).toBeGreaterThan(700);
});

test('reveal all shows the right column from the start', async ({ mount, page }) => {
  await mount(render(twoColumn(base), 0));
  await expect(page.getByTestId('two-column-right').locator('xpath=..')).toHaveAttribute(
    'data-revealed',
    'true',
  );
});

test('callout under the columns', async ({ mount, page }) => {
  await mount(
    render(twoColumn({ ...base, callout: { tone: 'info', text: 'Read both columns.' } })),
  );
  const callout = (await box(page, 'body-callout'))!;
  const left = (await box(page, 'two-column-left'))!;
  const right = (await box(page, 'two-column-right'))!;
  expect(callout.y).toBeGreaterThanOrEqual(left.y + left.height);
  expect(callout.y).toBeGreaterThanOrEqual(right.y + right.height);
  expect(callout.x).toBeCloseTo(left.x, 0);
  expect(callout.x + callout.width).toBeCloseTo(right.x + right.width, 0);
});

test('callout arrives on the step after the right column', async ({ mount, page }) => {
  const slots: TwoColumnSlots = {
    ...base,
    reveal: 'one-by-one',
    callout: { tone: 'info', text: 'Last', reveal: 'last' },
  };
  const component = await mount(render(twoColumn(slots), 1));
  const callout = page.getByTestId('body-callout').locator('.sw-reveal');
  await expect(callout).toHaveAttribute('data-revealed', 'false');
  await component.update(render(twoColumn(slots), 2));
  await expect(callout).toHaveAttribute('data-revealed', 'true');
});

test('icons in the slots reach both columns', async ({ mount, page }) => {
  await mount(
    render(
      twoColumn({
        headline: 'Icons',
        left: { title: 'Left', icon: 'bolt', items: [{ icon: 'check', text: 'Done' }, 'Plain'] },
        right: { title: 'Right', icon: 'clock', items: [{ icon: 'bolt', text: 'Fast' }] },
      }),
    ),
  );
  await expect(page.getByTestId('two-column-title-icon')).toHaveCount(2);
  await expect(page.getByTestId('two-column-item-icon')).toHaveCount(2);
});

test('an unknown icon name keeps the bullet', async ({ mount, page }) => {
  await mount(
    render(
      twoColumn({
        headline: 'Unknown',
        left: { items: [{ icon: 'no-such-icon', text: 'Mystery' }, 'Plain'] },
        right: { title: 'Right', icon: 'no-such-icon', text: 'Text' },
      }),
    ),
  );
  await expect(page.getByTestId('two-column-item-icon')).toHaveCount(0);
  await expect(page.getByTestId('two-column-title-icon')).toHaveCount(0);
  await expect(page.getByTestId('block-icon')).toHaveCount(0);
  const items = page.getByTestId('two-column-left').getByTestId('two-column-item');
  for (const li of [items.first(), items.nth(1)]) {
    expect(await li.evaluate((el) => getComputedStyle(el, '::before').content)).not.toBe('none');
  }
  const unknown = (await items.first().boundingBox())!;
  const plain = (await items.nth(1).boundingBox())!;
  expect(unknown.x).toBeCloseTo(plain.x, 0);
});

test('headline emphasis carries its tone', async ({ mount, page }) => {
  await mount(
    render(
      twoColumn({
        ...base,
        headline: 'Small steps beat big plans',
        emphasis: [{ text: 'big plans', tone: 'negative' }],
      }),
    ),
  );
  const phrase = page.getByTestId('emphasis');
  await expect(phrase).toHaveText('big plans');
  await expect(phrase).toHaveAttribute('data-tone', 'negative');
});

// The left and right extents, and the bottom, of every text run in the layout.
function textBox(page: Page) {
  return page.getByTestId('layout-two-column').evaluate((root: Element) => {
    const slide = root.closest('[data-layout]')!.getBoundingClientRect();
    let left = Infinity;
    let right = -Infinity;
    let bottom = -Infinity;
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
      const range = document.createRange();
      range.selectNodeContents(node);
      for (const rect of range.getClientRects()) {
        if (rect.width === 0) continue;
        left = Math.min(left, rect.left - slide.left);
        right = Math.max(right, rect.right - slide.left);
        bottom = Math.max(bottom, rect.bottom - slide.top);
      }
    }
    return { left, right, bottom };
  });
}

test('twoColumnCallout and its wide twin stay inside the box', async ({ mount, page }) => {
  await stubApi(page, {});
  const fixture = fitFixture('twoColumnCallout');
  const original = fixture.slots as TwoColumnSlots;
  const twin = widen(original) as TwoColumnSlots;
  for (const [name, slots] of [
    ['fixture', original],
    ['wide twin', twin],
  ] as const) {
    const component = await mount(render(twoColumn(slots), 2));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    if (name === 'fixture') expect(drawn.bottom).toBeLessThanOrEqual(996);
    else test.info().annotations.push({ type: 'wide twin bottom', description: `${drawn.bottom}` });
    await component.unmount();
  }
});

test('screenshot: two-column with callout and icons', async ({ mount }) => {
  const component = await mount(
    render(
      twoColumn({
        kicker: 'Slice it',
        headline: 'A story map shows the thinnest release',
        left: {
          title: 'How to read it',
          icon: 'target',
          items: [
            { icon: 'check', lead: 'One row.', text: 'One release.' },
            { icon: 'bolt', text: 'One column is one step.' },
          ],
        },
        right: {
          title: 'What to look for',
          icon: 'search',
          text: 'The thinnest slice that still ships.',
        },
        callout: { tone: 'info', text: 'Cut from the right, keep the walking skeleton.' },
        emphasis: [{ text: 'thinnest release', tone: 'accent' }],
      }),
    ),
  );
  await expect(component).toHaveScreenshot('layout-two-column-callout-icons.png');
});
