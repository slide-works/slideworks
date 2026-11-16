import { test, expect } from '@playwright/experimental-ct-react';
import type { Locator, Page } from 'playwright/test';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { SlideDeck } from '../slots';

// The title, divider, close, quote and big-number layouts with the stamp and
// journey blocks, rendered through SlideRenderer inside the real frame.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function sample(number: number): RenderableSlide {
  const slide = sampleDeck.slides.find((s) => s.number === number);
  if (slide === undefined) throw new Error(`sample deck has no slide ${number}`);
  return slide as RenderableSlide;
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, slideDeck: SlideDeck = deck) {
  return <SlideRenderer slide={slide} deck={slideDeck} total={sampleDeck.slides.length} step={0} />;
}

// Text of exactly `length` characters made of ordinary words, wide letters first.
function fill(length: number): string {
  const words = ['Maximum', 'Width', 'words', 'wrap', 'across', 'the', 'whole', 'slide'];
  let text = '';
  for (let i = 0; text.length < length; i++) text += (text ? ' ' : '') + words[i % words.length];
  return text.slice(0, length).trimEnd().padEnd(length, 'm');
}

// Text of exactly `length` characters in the widest letters: capitals, mostly
// W and M, in words a line can still break between.
function wide(length: number): string {
  const words = ['WWWWWW', 'MAXIMUM', 'WMW', 'MMMMMM', 'WOMBAT', 'WWW'];
  let text = '';
  for (let i = 0; text.length < length; i++) text += (text ? ' ' : '') + words[i % words.length];
  return text.slice(0, length).trimEnd().padEnd(length, 'W');
}

// The font size an element is drawn at, in px.
async function fontSize(page: Page, testId: string): Promise<number> {
  await page.evaluate(() => document.fonts.ready);
  return page
    .getByTestId(testId)
    .evaluate((element: Element) => parseFloat(getComputedStyle(element).fontSize));
}

// How many lines of `lineHeight` × its font size an element takes.
async function lineCount(page: Page, testId: string, lineHeight: number): Promise<number> {
  const size = await fontSize(page, testId);
  const height = await page
    .getByTestId(testId)
    .evaluate((element: Element) => (element as HTMLElement).offsetHeight);
  return Math.round(height / (size * lineHeight));
}

// How many 18 px/1.25 lines each journey part name takes.
async function nameLines(names: Locator): Promise<number[]> {
  await names.page().evaluate(() => document.fonts.ready);
  const heights = await names.evaluateAll((elements) =>
    elements.map((element) => (element as HTMLElement).offsetHeight),
  );
  return heights.map((height) => Math.round(height / (18 * 1.25)));
}

type Box = { left: number; top: number; right: number; bottom: number };

// The extent of everything a layout draws, in canvas px from the slide's top
// left: every element's box and every run of text (text that overflows its
// element is caught by its own rects). Text hidden by an ancestor that clips
// its overflow, such as a clamped journey name, is cut to that ancestor's box.
// Elements matching `exclude` (and what is inside them) are left out.
async function drawnBox(page: Page, layoutTestId: string, exclude?: string): Promise<Box> {
  await page.evaluate(() => document.fonts.ready);
  return page.getByTestId(layoutTestId).evaluate((root: Element, excluded: string | undefined) => {
    const slide = root.closest('[data-layout]')!.getBoundingClientRect();
    const box = { left: Infinity, top: Infinity, right: -Infinity, bottom: -Infinity };
    const skip = (element: Element | null) =>
      excluded !== undefined && element?.closest(excluded) != null;
    const add = (rect: { left: number; top: number; right: number; bottom: number }) => {
      if (rect.right - rect.left <= 0 && rect.bottom - rect.top <= 0) return;
      box.left = Math.min(box.left, rect.left - slide.left);
      box.top = Math.min(box.top, rect.top - slide.top);
      box.right = Math.max(box.right, rect.right - slide.left);
      box.bottom = Math.max(box.bottom, rect.bottom - slide.top);
    };
    for (const element of root.querySelectorAll('*')) {
      if (!skip(element)) add(element.getBoundingClientRect());
    }
    // A line's glyphs can rise a little above its line box (a 20 px/1 kicker,
    // the 240 px/0.9 part number) while the ink stays inside it, so the top is
    // taken from the boxes, as in ContentLayouts.spec.tsx.
    const boxTop = box.top;
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
      if (skip(node.parentElement)) continue;
      const clips: DOMRect[] = [];
      for (let el = node.parentElement; el !== null && el !== root; el = el.parentElement) {
        if (getComputedStyle(el).overflow !== 'visible') clips.push(el.getBoundingClientRect());
      }
      const range = document.createRange();
      range.selectNodeContents(node);
      for (const rect of range.getClientRects()) {
        let { left, top, right, bottom } = rect;
        for (const clip of clips) {
          left = Math.max(left, clip.left);
          top = Math.max(top, clip.top);
          right = Math.min(right, clip.right);
          bottom = Math.min(bottom, clip.bottom);
        }
        if (right > left && bottom > top) add({ left, top, right, bottom });
      }
    }
    return { ...box, top: boxTop };
  }, exclude);
}

// The content box is x 120–1800 from y = 150; a body may run to y = 996,
// above the footer at 1023.
function expectInsideContent(box: Box) {
  expect(box.left).toBeGreaterThanOrEqual(120);
  expect(box.right).toBeLessThanOrEqual(1800);
  expect(box.top).toBeGreaterThanOrEqual(150);
  expect(box.bottom).toBeLessThanOrEqual(996);
}

test('title renders kicker title subtitle author stamp', async ({ mount, page }) => {
  await mount(render(sample(1)));

  const layout = page.getByTestId('layout-title');
  await expect(layout.getByTestId('title-kicker')).toHaveText('Team offsite · 2026');
  await expect(layout.getByRole('heading', { level: 1 })).toHaveText('Ship smaller');
  await expect(layout.getByTestId('title-subtitle')).toHaveText(
    'Why small batches beat big plans, and how to start on Monday',
  );
  await expect(layout.getByTestId('title-tagline')).toHaveText(
    'One change a day beats one launch a quarter.',
  );
  await expect(layout.getByTestId('title-byline')).toHaveText('Sam RiveraProduct Engineering');
  await expect(layout.getByTestId('block-stamp')).toHaveText('Draft');
  await expect(page.getByTestId('slide-frame')).toHaveAttribute('data-dark', 'true');
  // A sample title keeps the mockup's size.
  expect(await fontSize(page, 'title-title')).toBe(168);
});

test('title leaves out the slots it is not given', async ({ mount, page }) => {
  await mount(
    render({
      number: 1,
      layout: 'title',
      part: null,
      deepDive: false,
      slots: { title: 'Ship smaller' },
    }),
  );

  const layout = page.getByTestId('layout-title');
  await expect(layout.getByRole('heading', { level: 1 })).toHaveText('Ship smaller');
  for (const id of ['title-kicker', 'title-subtitle', 'title-tagline', 'title-byline']) {
    await expect(layout.getByTestId(id)).toHaveCount(0);
  }
  await expect(layout.getByTestId('block-stamp')).toHaveCount(0);
});

test('divider shows part number and journey state', async ({ mount, page }) => {
  const component = await mount(render(sample(2)));

  await expect(page.getByTestId('divider-number')).toHaveText('01');
  await expect(page.getByTestId('divider-title')).toHaveText('Big batches hide the risk');
  await expect(page.getByTestId('divider-sub')).toHaveText(
    'Why the quarter-long launch keeps slipping.',
  );
  const parts = page.getByTestId('journey-part');
  await expect(parts.locator('b')).toHaveText(deck.parts);
  await expect(parts.nth(0)).toHaveAttribute('data-state', 'on');
  await expect(parts.nth(0)).toHaveAttribute('aria-current', 'step');
  await expect(parts.nth(1)).toHaveAttribute('data-state', 'next');

  await component.update(render(sample(7)));
  await expect(page.getByTestId('divider-number')).toHaveText('02');
  await expect(parts.nth(0)).toHaveAttribute('data-state', 'past');
  await expect(parts.nth(1)).toHaveAttribute('data-state', 'on');
  await expect(parts.nth(2)).toHaveAttribute('data-state', 'next');
  await expect(parts.nth(3)).toHaveAttribute('data-state', 'next');
  await expect(page.locator('[aria-current]')).toHaveCount(1);
});

test('divider outside any part shows no number and no current part', async ({ mount, page }) => {
  await mount(render({ ...sample(2), part: null }));

  await expect(page.getByTestId('divider-number')).toHaveCount(0);
  await expect(page.locator('[data-testid="journey-part"][data-state="next"]')).toHaveCount(4);
  await expect(page.locator('[aria-current]')).toHaveCount(0);
});

test('divider in a deck without parts shows no journey', async ({ mount, page }) => {
  await mount(render({ ...sample(2), part: null }, { ...deck, parts: [] }));

  await expect(page.getByTestId('divider-title')).toHaveText('Big batches hide the risk');
  await expect(page.getByTestId('block-journey')).toHaveCount(0);
});

test('close splits lines', async ({ mount, page }) => {
  await mount(render(sample(16)));

  await expect(page.getByTestId('close-line')).toHaveText([
    'Pick one story.',
    'Ship half of it by Friday.',
  ]);
  await expect(page.getByTestId('close-sub')).toHaveText(
    'Then tell us what you learned at the next demo.',
  );
  await expect(page.getByTestId('close-author')).toHaveText('Sam Rivera · Product Engineering');
  const [first, second] = await page
    .getByTestId('close-line')
    .evaluateAll((lines) => lines.map((line) => line.getBoundingClientRect().top));
  expect(second).toBeGreaterThan(first);
});

test('quote and attribution', async ({ mount, page }) => {
  await mount(render(sample(6)));

  await expect(page.getByTestId('quote-kicker')).toHaveText('From the retro');
  await expect(page.getByTestId('quote-text')).toHaveText(
    '“We stopped asking ‘is it done?’ and started asking ‘is it out?’ The second question is much harder to fudge.”',
  );
  await expect(page.getByTestId('quote-attribution')).toHaveText('Team lead, payments squad');
  await expect(page.getByTestId('slide-footer')).toBeVisible();
  // The quote opens with the shared Kicker block: 20 px/1 at the content top.
  const frame = (await page.getByTestId('slide-frame').boundingBox())!;
  const kicker = (await page.getByTestId('quote-kicker').boundingBox())!;
  expect(kicker.y - frame.y).toBe(150);
  expect(kicker.height).toBe(20);
  expect(await fontSize(page, 'quote-kicker')).toBe(20);
});

test('big number value caption source', async ({ mount, page }) => {
  await mount(render(sample(4)));

  await expect(page.getByTestId('big-number-kicker')).toHaveText('Last year, measured');
  await expect(page.getByTestId('big-number-headline')).toHaveText(
    'It took us eleven weeks to get a change in front of a customer',
  );
  const value = page.getByTestId('big-number-value');
  await expect(value).toHaveText('11 wks');
  await expect(value.locator('small')).toHaveText(' wks');
  await expect(page.getByTestId('big-number-caption')).toHaveText(
    'Median from first commit to the first customer using it.',
  );
  await expect(page.getByTestId('big-number-detail')).toHaveText(
    'Eight of our 12 launches waited on one last piece of work that had nothing to do with the rest.',
  );
  await expect(page.getByTestId('big-number-source')).toHaveText(
    'Source: Delivery data Jan–Oct, 12 launches.',
  );
  // A value that fits keeps the mockup's size, and so do the caption and detail.
  expect(await fontSize(page, 'big-number-value')).toBe(300);
  expect(await fontSize(page, 'big-number-caption')).toBe(40);
  expect(await fontSize(page, 'big-number-detail')).toBe(28);
});

test('big number shrinks a 6-digit value to fit its column', async ({ mount, page }) => {
  await mount(
    render({
      ...sample(4),
      slots: { ...(sample(4).slots as object), value: '888888' },
    } as RenderableSlide),
  );

  const value = page.getByTestId('big-number-value');
  await expect(value).toHaveText('888888');
  const size = await fontSize(page, 'big-number-value');
  expect(size).toBeLessThan(300);
  expect(size).toBeGreaterThanOrEqual(120);

  const column = (await page.getByTestId('big-number-column').boundingBox())!;
  const box = (await value.boundingBox())!;
  expect(column.width).toBe(820);
  expect(box.x).toBeGreaterThanOrEqual(column.x);
  expect(box.x + box.width).toBeLessThanOrEqual(column.x + column.width);
  expectInsideContent(await drawnBox(page, 'layout-big-number'));
});

test('the value fits once a thumbnail that starts at scale 0 gets its scale', async ({
  mount,
  page,
}) => {
  // The deck page's thumbnail draws the slide at scale(0) until it knows its
  // width, so the fit waits for a box that measures more than 0.
  const slide = { ...sample(4), slots: { ...(sample(4).slots as object), value: '888888' } };
  await mount(
    <div data-testid="thumb" style={{ transform: 'scale(0)', transformOrigin: '0 0' }}>
      {render(slide as RenderableSlide)}
    </div>,
  );
  await page.evaluate(() => document.fonts.ready);
  await page.evaluate(
    () => new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(done))),
  );
  await page.getByTestId('thumb').evaluate((thumb: HTMLElement) => {
    thumb.style.transform = 'scale(0.25)';
  });

  await expect.poll(() => fontSize(page, 'big-number-value')).toBeLessThan(300);
  const column = (await page.getByTestId('big-number-column').boundingBox())!;
  const box = (await page.getByTestId('big-number-value').boundingBox())!;
  expect(box.x + box.width).toBeLessThanOrEqual(column.x + column.width);
});

test('the value fits once a thumbnail hidden for 2 s is shown', async ({ mount, page }) => {
  // A slide inside a display: none container has no layout size to fit
  // against, so the fit waits for one however long that takes; it used to
  // give up after about a second.
  const slide = { ...sample(4), slots: { ...(sample(4).slots as object), value: '888888' } };
  await mount(
    <div data-testid="thumb" style={{ display: 'none' }}>
      {render(slide as RenderableSlide)}
    </div>,
  );
  await page.evaluate(() => document.fonts.ready);
  await page.waitForTimeout(2000);
  expect(await fontSize(page, 'big-number-value')).toBe(300);

  await page.getByTestId('thumb').evaluate((thumb: HTMLElement) => {
    thumb.style.display = 'block';
  });

  await expect.poll(() => fontSize(page, 'big-number-value')).toBeLessThan(300);
  const column = (await page.getByTestId('big-number-column').boundingBox())!;
  const box = (await page.getByTestId('big-number-value').boundingBox())!;
  expect(box.x + box.width).toBeLessThanOrEqual(column.x + column.width);
});

test('each layout root carries the sw-layout seam, and big-number its sw-body', async ({
  mount,
  page,
}) => {
  const layouts = { 1: 'title', 2: 'divider', 4: 'big-number', 6: 'quote', 16: 'close' };
  for (const [number, layout] of Object.entries(layouts)) {
    const component = await mount(render(sample(Number(number))));
    await expect(page.getByTestId(`layout-${layout}`)).toHaveClass(/(^| )sw-layout( |$)/);
    await component.unmount();
  }
  await mount(render(sample(4)));
  await expect(page.locator('[data-testid="layout-big-number"] > .sw-body')).toHaveCount(1);
});

// Each layout at its slots' longest lengths (layouts.md), once in prose and once
// in the widest letters. Everything drawn stays inside x 120–1800 and y 150–996;
// the title's stamp, which sits in the top margin, is checked on its own.
test.describe('max-length text stays inside 1920x1080', () => {
  for (const [letters, text] of [
    ['prose', fill],
    ['wide letters', wide],
  ] as const) {
    test(`title in ${letters}`, async ({ mount, page }) => {
      await mount(
        render({
          number: 1,
          layout: 'title',
          part: null,
          deepDive: false,
          slots: {
            kicker: text(40),
            title: text(34),
            subtitle: text(100),
            tagline: text(90),
            author: text(60),
            affiliation: text(60),
            stamp: 'M'.repeat(16),
          },
        }),
      );

      expectInsideContent(await drawnBox(page, 'layout-title', '[data-testid="block-stamp"]'));
      // The title is held to two lines, from 168 px down to 96 px.
      const size = await fontSize(page, 'title-title');
      expect(size).toBeGreaterThanOrEqual(96);
      expect(await lineCount(page, 'title-title', 0.95)).toBeLessThanOrEqual(2);
      if (letters === 'wide letters') expect(size).toBeLessThan(168);
      const stamp = (await page.getByTestId('block-stamp').boundingBox())!;
      expect(stamp.x + stamp.width).toBeLessThanOrEqual(1800 + 4);
      expect(stamp.y).toBeGreaterThanOrEqual(8);
      // The stamp sits above the content, clear of the title text.
      const kicker = (await page.getByTestId('title-kicker').boundingBox())!;
      expect(stamp.y + stamp.height).toBeLessThanOrEqual(kicker.y);
    });

    if (letters === 'wide letters') {
      // Every slot at its longest in capital W alone, the widest text there is:
      // the subtitle then takes four lines at 52 px and pushed the byline to
      // y 1055 before it had a fit.
      test('title in capital W', async ({ mount, page }) => {
        const w = (length: number) => 'W'.repeat(length);
        await mount(
          render({
            number: 1,
            layout: 'title',
            part: null,
            deepDive: false,
            slots: {
              kicker: w(40),
              title: w(34),
              subtitle: w(100),
              tagline: w(90),
              author: w(60),
              affiliation: w(60),
              stamp: w(16),
            },
          }),
        );

        const box = await drawnBox(page, 'layout-title', '[data-testid="block-stamp"]');
        expectInsideContent(box);
        // The subtitle is held to three lines, from 52 px down to 36 px.
        const size = await fontSize(page, 'title-subtitle');
        expect(size).toBeLessThan(52);
        expect(size).toBeGreaterThanOrEqual(36);
        expect(await lineCount(page, 'title-subtitle', 1.15)).toBeLessThanOrEqual(3);
      });

      // A prose title keeps 168 px and two lines, so the slots under it get only
      // the room it leaves. In capitals, every other slot at its longest takes
      // the subtitle to three lines at 52 px, and the byline ended at y 1052.
      test('title, prose title with ALL-CAPS slots', async ({ mount, page }) => {
        const caps = (length: number) => fill(length).toUpperCase();
        await mount(
          render({
            number: 1,
            layout: 'title',
            part: null,
            deepDive: false,
            slots: {
              kicker: caps(40),
              title: fill(34),
              subtitle: caps(100),
              tagline: caps(90),
              author: caps(60),
              affiliation: caps(60),
              stamp: caps(16),
            },
          }),
        );

        expectInsideContent(await drawnBox(page, 'layout-title', '[data-testid="block-stamp"]'));
        expect(await fontSize(page, 'title-title')).toBe(168);
        // The subtitle shrinks into the room left, still at most three lines.
        const size = await fontSize(page, 'title-subtitle');
        expect(size).toBeLessThan(52);
        expect(size).toBeGreaterThanOrEqual(36);
        expect(await lineCount(page, 'title-subtitle', 1.15)).toBeLessThanOrEqual(3);
      });

      // Words of capital W in the tagline and byline take the tagline to three
      // lines and the byline to two; under a prose title the byline ended at
      // y 1070, more than the subtitle can give back at 36 px.
      test('title, prose title with W-word tagline and byline', async ({ mount, page }) => {
        const words = (length: number) =>
          'WWWWWWWWW '
            .repeat(Math.ceil(length / 10))
            .slice(0, length)
            .trimEnd()
            .padEnd(length, 'W');
        await mount(
          render({
            number: 1,
            layout: 'title',
            part: null,
            deepDive: false,
            slots: {
              kicker: fill(40),
              title: fill(34),
              subtitle: fill(100),
              tagline: words(90),
              author: words(60),
              affiliation: words(60),
              stamp: 'M'.repeat(16),
            },
          }),
        );

        expectInsideContent(await drawnBox(page, 'layout-title', '[data-testid="block-stamp"]'));
        expect(await fontSize(page, 'title-title')).toBe(168);
        // The tagline shrinks into the room left, from 34 px down to 26 px.
        const size = await fontSize(page, 'title-tagline');
        expect(size).toBeLessThan(34);
        expect(size).toBeGreaterThanOrEqual(26);
      });
    }

    test(`divider with eight long parts in ${letters}`, async ({ mount, page }) => {
      const parts = Array.from({ length: 8 }, () => text(40));
      await mount(
        render(
          {
            number: 2,
            layout: 'divider',
            part: 8,
            deepDive: false,
            slots: { title: text(44), sub: text(80) },
          },
          { ...deck, parts },
        ),
      );

      await expect(page.getByTestId('divider-number')).toHaveText('08');
      expectInsideContent(await drawnBox(page, 'layout-divider'));
      // The title is held to two lines, from 104 px down to 64 px.
      const size = await fontSize(page, 'divider-title');
      expect(size).toBeGreaterThanOrEqual(64);
      expect(await lineCount(page, 'divider-title', 1.02)).toBeLessThanOrEqual(2);
      if (letters === 'wide letters') expect(size).toBeLessThan(104);
      // A long part name is cut to two lines; the whole name is its title.
      const name = page.getByTestId('journey-part').first().locator('b');
      await expect(name).toHaveAttribute('title', parts[0]);
      const names = page.getByTestId('journey-part').locator('b');
      expect(Math.max(...(await nameLines(names)))).toBeLessThanOrEqual(2);
      // The probe: unclamped, the same names run past two lines, so the
      // measure above would catch a lost clamp.
      await names.evaluateAll((elements) => {
        for (const element of elements) (element as HTMLElement).style.webkitLineClamp = '99';
      });
      expect(Math.max(...(await nameLines(names)))).toBeGreaterThan(2);
    });

    test(`close in ${letters}`, async ({ mount, page }) => {
      // The longest headline the validator accepts: three written lines that it
      // estimates at four shown lines.
      const headline =
        letters === 'prose'
          ? 'Pick one story today.\nShip half by Friday.\nThen tell us what you learned there'
          : `${'W'.repeat(22)}\n${'W'.repeat(22)}\n${'W'.repeat(17)} ${'W'.repeat(16)}`;
      expect(headline.length).toBeLessThanOrEqual(80);
      await mount(
        render({
          number: 16,
          layout: 'close',
          part: 4,
          deepDive: false,
          slots: { headline, sub: text(110), author: text(60) },
        }),
      );

      expectInsideContent(await drawnBox(page, 'layout-close'));
      // The headline is held to four lines, from 112 px down to 60 px.
      const size = await fontSize(page, 'close-headline');
      expect(size).toBeGreaterThanOrEqual(60);
      expect(await lineCount(page, 'close-headline', 1.02)).toBeLessThanOrEqual(4);
      if (letters === 'wide letters') expect(size).toBeLessThan(112);
    });

    test(`quote in ${letters}`, async ({ mount, page }) => {
      await mount(
        render({
          number: 6,
          layout: 'quote',
          part: 1,
          deepDive: false,
          slots: { kicker: text(40), quote: text(280), attribution: text(80) },
        }),
      );

      expectInsideContent(await drawnBox(page, 'layout-quote'));
    });

    test(`big-number in ${letters}`, async ({ mount, page }) => {
      await mount(
        render({
          number: 4,
          layout: 'big-number',
          part: 1,
          deepDive: false,
          slots: {
            kicker: text(40),
            headline: text(80),
            value: letters === 'prose' ? '888888' : 'WWWWWW',
            caption: text(120),
            detail: text(200),
            source: text(100),
          },
        }),
      );

      expectInsideContent(await drawnBox(page, 'layout-big-number'));
    });
  }
});

test('screenshot: title, divider, close, quote and big-number', async ({ mount, page }) => {
  for (const number of [1, 2, 4, 6, 16]) {
    const component = await mount(render(sample(number)));
    await page.evaluate(() => document.fonts.ready);
    await expect(page.getByTestId(`slide-${number}`)).toHaveScreenshot(
      `statement-layout-${number}.png`,
    );
    await component.unmount();
  }
});
