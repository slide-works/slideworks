import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
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

type Box = { left: number; top: number; right: number; bottom: number };

// The extent of everything a layout draws, in canvas px from the slide's top
// left: every element's box and every run of text (text that overflows its
// element is caught by its own rects).
async function drawnBox(page: Page, layoutTestId: string): Promise<Box> {
  await page.evaluate(() => document.fonts.ready);
  return page.getByTestId(layoutTestId).evaluate((root: Element) => {
    const slide = root.closest('[data-layout]')!.getBoundingClientRect();
    const box = { left: Infinity, top: Infinity, right: -Infinity, bottom: -Infinity };
    const add = (rect: DOMRect) => {
      if (rect.width === 0 && rect.height === 0) return;
      box.left = Math.min(box.left, rect.left - slide.left);
      box.top = Math.min(box.top, rect.top - slide.top);
      box.right = Math.max(box.right, rect.right - slide.left);
      box.bottom = Math.max(box.bottom, rect.bottom - slide.top);
    };
    for (const element of root.querySelectorAll('*')) add(element.getBoundingClientRect());
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
      const range = document.createRange();
      range.selectNodeContents(node);
      for (const rect of range.getClientRects()) add(rect);
    }
    return box;
  });
}

// The content box is x 120–1800; a body may run to y = 996, above the footer at 1023.
function expectInsideContent(box: Box, top = 150) {
  expect(box.left).toBeGreaterThanOrEqual(120);
  expect(box.right).toBeLessThanOrEqual(1800);
  expect(box.top).toBeGreaterThanOrEqual(top);
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
  await page.evaluate(() => document.fonts.ready);
  await expect(layout.getByTestId('title-title')).toHaveAttribute('data-font-size', '168');
});

test('title leaves out the slots it is not given', async ({ mount, page }) => {
  await mount(render({ number: 1, layout: 'title', part: null, slots: { title: 'Ship smaller' } }));

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
  // A value that fits keeps the mockup's size.
  await page.evaluate(() => document.fonts.ready);
  await expect(value).toHaveAttribute('data-font-size', '300');
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
  await page.evaluate(() => document.fonts.ready);
  await expect
    .poll(async () => Number(await value.getAttribute('data-font-size')))
    .toBeLessThan(300);
  const size = Number(await value.getAttribute('data-font-size'));
  expect(size).toBeGreaterThanOrEqual(120);

  const column = (await page.getByTestId('big-number-column').boundingBox())!;
  const box = (await value.boundingBox())!;
  expect(column.width).toBe(820);
  expect(box.x).toBeGreaterThanOrEqual(column.x);
  expect(box.x + box.width).toBeLessThanOrEqual(column.x + column.width);
  expectInsideContent(await drawnBox(page, 'layout-big-number'));
});

test.describe('max-length text stays inside 1920x1080', () => {
  test('title', async ({ mount, page }) => {
    await mount(
      render({
        number: 1,
        layout: 'title',
        part: null,
        slots: {
          kicker: fill(40),
          title: fill(34),
          subtitle: fill(100),
          tagline: fill(90),
          author: fill(60),
          affiliation: fill(60),
          stamp: 'M'.repeat(16),
        },
      }),
    );

    expectInsideContent(await drawnBox(page, 'layout-title'), 0);
    // A wide 34-character title is held to two lines.
    const title = page.getByTestId('title-title');
    const size = Number(await title.getAttribute('data-font-size'));
    expect(size).toBeGreaterThanOrEqual(96);
    expect((await title.boundingBox())!.height).toBeLessThanOrEqual(2 * size * 0.95 + 1);
    const stamp = (await page.getByTestId('block-stamp').boundingBox())!;
    expect(stamp.x + stamp.width).toBeLessThanOrEqual(1800 + 4);
    expect(stamp.y).toBeGreaterThanOrEqual(8);
    // The stamp sits above the content, clear of the title text.
    const kicker = (await page.getByTestId('title-kicker').boundingBox())!;
    expect(stamp.y + stamp.height).toBeLessThanOrEqual(kicker.y);
  });

  test('divider with eight long parts', async ({ mount, page }) => {
    const parts = Array.from({ length: 8 }, () => fill(40));
    await mount(
      render(
        { number: 2, layout: 'divider', part: 8, slots: { title: fill(44), sub: fill(80) } },
        { ...deck, parts },
      ),
    );

    await expect(page.getByTestId('divider-number')).toHaveText('08');
    expectInsideContent(await drawnBox(page, 'layout-divider'), 0);
  });

  test('close', async ({ mount, page }) => {
    // Three written lines that wrap to four shown lines, the validator's limit.
    const headline =
      'Pick one story today.\nShip half by Friday.\nThen tell us what you learned there';
    expect(headline.length).toBe(78);
    await mount(
      render({
        number: 16,
        layout: 'close',
        part: 4,
        slots: { headline, sub: fill(110), author: fill(60) },
      }),
    );

    expectInsideContent(await drawnBox(page, 'layout-close'), 0);
  });

  test('quote', async ({ mount, page }) => {
    await mount(
      render({
        number: 6,
        layout: 'quote',
        part: 1,
        slots: { kicker: fill(40), quote: fill(280), attribution: fill(80) },
      }),
    );

    expectInsideContent(await drawnBox(page, 'layout-quote'));
  });

  test('big-number', async ({ mount, page }) => {
    await mount(
      render({
        number: 4,
        layout: 'big-number',
        part: 1,
        slots: {
          kicker: fill(40),
          headline: fill(80),
          value: '888888',
          caption: fill(120),
          detail: fill(200),
          source: fill(100),
        },
      }),
    );

    expectInsideContent(await drawnBox(page, 'layout-big-number'));
  });
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
