import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { StatementSlots } from '../slots/statement';
import type { SlideDeck } from '../slots';

// The statement layout: one big sentence, light or dark, with a sub line that
// can arrive on a step, emphasis, and no tracker.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

// In a real part, to prove a deck with parts still draws no tracker here.
function statement(slots: StatementSlots, surface?: 'light' | 'dark'): RenderableSlide {
  return { number: 3, layout: 'statement', part: 1, deepDive: false, surface, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const base: StatementSlots = {
  kicker: 'The point',
  statement: 'Small batches are\nhow we find out sooner.',
  sub: 'Ship half of it by Friday, then learn from the rest.',
};

// The computed colour of a palette token inside an element.
function tokenColour(page: Page, testId: string, key: string): Promise<string> {
  return page
    .getByTestId(testId)
    .first()
    .evaluate((element, token) => {
      const probe = document.createElement('span');
      probe.style.color = `var(--sw-${token})`;
      element.appendChild(probe);
      const colour = getComputedStyle(probe).color;
      probe.remove();
      return colour;
    }, key);
}

function colourOf(page: Page, testId: string): Promise<string> {
  return page
    .getByTestId(testId)
    .first()
    .evaluate((el) => getComputedStyle(el).color);
}

test('statement light and dark', async ({ mount, page }) => {
  const component = await mount(render(statement(base, 'light')));
  expect(await colourOf(page, 'statement-text')).toBe(
    await tokenColour(page, 'statement-text', 'ink'),
  );
  const light = await colourOf(page, 'statement-text');
  await component.update(render(statement(base, 'dark')));
  const dark = await colourOf(page, 'statement-text');
  expect(dark).toBe(await tokenColour(page, 'statement-text', 'night-ink'));
  expect(dark).not.toBe(light);
  expect(await colourOf(page, 'statement-sub')).toBe(
    await tokenColour(page, 'statement-sub', 'accent-glow'),
  );
});

test('statement draws no tracker but keeps the footer', async ({ mount, page }) => {
  await mount(render(statement(base)));
  await expect(page.getByTestId('part-tracker')).toHaveCount(0);
  await expect(page.getByTestId('slide-footer')).toHaveCount(1);
});

function box(page: Page, testId: string) {
  return page.getByTestId(testId).evaluate((el) => {
    const rect = el.getBoundingClientRect();
    return { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
  });
}

test('sub hidden at step 0 keeps its space', async ({ mount, page }) => {
  const slots: StatementSlots = { ...base, reveal: 'one-by-one' };
  await stubApi(page, {});
  const component = await mount(render(statement(slots), 0));
  await page.evaluate(() => document.fonts.ready);
  const wrapper = page.getByTestId('statement-sub').locator('xpath=..');
  await expect(wrapper).toHaveAttribute('data-revealed', 'false');
  const hidden = await box(page, 'statement-sub');
  await component.update(render(statement(slots), 1));
  await expect(wrapper).toHaveAttribute('data-revealed', 'true');
  await page.evaluate(() => document.fonts.ready);
  expect(await box(page, 'statement-sub')).toEqual(hidden);
});

test('a sub without reveal shows with the slide', async ({ mount, page }) => {
  await mount(render(statement(base), 0));
  await expect(page.getByTestId('statement-sub')).toBeVisible();
  await expect(page.locator('.sw-reveal')).toHaveCount(0);
});

test('emphasis in the sentence is accent-glow on dark', async ({ mount, page }) => {
  await mount(
    render(statement({ ...base, emphasis: [{ text: 'find out sooner', tone: 'accent' }] }, 'dark')),
  );
  const phrase = page.getByTestId('statement-text').getByTestId('emphasis');
  await expect(phrase).toHaveText('find out sooner');
  expect(await phrase.evaluate((el) => getComputedStyle(el).color)).toBe(
    await tokenColour(page, 'statement-text', 'accent-glow'),
  );
  await expect(page.getByTestId('statement-line')).toHaveText([
    'Small batches are',
    'how we find out sooner.',
  ]);
});

test('emphasis names the sub', async ({ mount, page }) => {
  await mount(
    render(statement({ ...base, emphasis: [{ text: 'learn', tone: 'positive', in: 'sub' }] })),
  );
  await expect(page.getByTestId('statement-text').getByTestId('emphasis')).toHaveCount(0);
  await expect(page.getByTestId('statement-sub').getByTestId('emphasis')).toHaveText('learn');
});

test('font size stays between 110 and 210 px', async ({ mount, page }) => {
  await stubApi(page, {});
  const component = await mount(render(statement({ statement: 'Short' })));
  await page.evaluate(() => document.fonts.ready);
  const size = () =>
    page.getByTestId('statement-text').evaluate((el) => parseFloat(getComputedStyle(el).fontSize));
  expect(await size()).toBe(210);
  await component.update(render(statement(fitFixture('statementMaxWide').slots as StatementSlots)));
  await page.evaluate(() => document.fonts.ready);
  const shrunk = await size();
  expect(shrunk).toBeGreaterThanOrEqual(110);
  expect(shrunk).toBeLessThanOrEqual(210);
});

async function drawn(page: Page) {
  return page.getByTestId('layout-statement').evaluate((root: Element) => {
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

for (const name of ['statementMax', 'statementMaxWide']) {
  test(`${name} stays inside the box`, async ({ mount, page }) => {
    await stubApi(page, {});
    await mount(render(statement(fitFixture(name).slots as StatementSlots)));
    await page.evaluate(() => document.fonts.ready);
    const box = await drawn(page);
    expect(box.left).toBeGreaterThanOrEqual(120);
    expect(box.right).toBeLessThanOrEqual(1800);
    expect(box.bottom).toBeLessThanOrEqual(996);
  });
}

test('screenshot: statement light', async ({ mount }) => {
  const component = await mount(render(statement(base, 'light')));
  await expect(component).toHaveScreenshot('layout-statement-light.png');
});

test('screenshot: statement dark', async ({ mount }) => {
  const component = await mount(
    render(statement({ ...base, emphasis: [{ text: 'find out sooner', tone: 'accent' }] }, 'dark')),
  );
  await expect(component).toHaveScreenshot('layout-statement-dark.png');
});

test('screenshot: statement max', async ({ mount }) => {
  const component = await mount(
    render(statement(fitFixture('statementMax').slots as StatementSlots)),
  );
  await expect(component).toHaveScreenshot('layout-statement-max.png');
});
