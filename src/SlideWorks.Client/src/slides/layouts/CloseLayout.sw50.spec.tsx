import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { CloseSlots } from '../slots/close';
import type { SlideDeck } from '../slots';

// The close layout's SW-50 addition: emphasis in the headline, applied per
// written line, and in the sub. It is a dark slide, so accent draws as the glow.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

// In a real part, like any other slide of a deck (close itself draws no tracker).
function slideOf(slots: CloseSlots): RenderableSlide {
  return { number: 3, layout: 'close', part: 1, deepDive: false, surface: undefined, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={0} />;
}

const base: CloseSlots = {
  headline: 'Pick one story.\nShip half of it by Friday.',
  sub: 'Then tell us what you learned at the next demo.',
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

test('close headline emphasis', async ({ mount, page }) => {
  await mount(render(slideOf({ ...base, emphasis: [{ text: 'half of it', tone: 'accent' }] })));
  const phrase = page.getByTestId('close-headline').getByTestId('emphasis');
  await expect(phrase).toHaveText('half of it');
  await expect(phrase).toHaveAttribute('data-tone', 'accent');
  const colour = await phrase.evaluate((el) => getComputedStyle(el).color);
  expect(colour).toBe(await tokenColour(page, 'close-headline', 'accent-glow'));
  // The headline's text is still the text, in its two written lines.
  await expect(page.getByTestId('close-line')).toHaveText([
    'Pick one story.',
    'Ship half of it by Friday.',
  ]);
});

test('close emphasis on the second written line', async ({ mount, page }) => {
  await mount(render(slideOf({ ...base, emphasis: [{ text: 'Friday', tone: 'positive' }] })));
  await expect(page.getByTestId('close-line').nth(0).getByTestId('emphasis')).toHaveCount(0);
  const phrase = page.getByTestId('close-line').nth(1).getByTestId('emphasis');
  await expect(phrase).toHaveText('Friday');
  await expect(phrase).toHaveAttribute('data-tone', 'positive');
});

test('a phrase that spans a written line break is drawn plain', async ({ mount, page }) => {
  await mount(render(slideOf({ ...base, emphasis: [{ text: 'story.\nShip', tone: 'accent' }] })));
  await expect(page.getByTestId('emphasis')).toHaveCount(0);
  await expect(page.getByTestId('close-line')).toHaveText([
    'Pick one story.',
    'Ship half of it by Friday.',
  ]);
});

test('emphasis names the sub', async ({ mount, page }) => {
  await mount(
    render(slideOf({ ...base, emphasis: [{ text: 'learned', tone: 'accent', in: 'sub' }] })),
  );
  await expect(page.getByTestId('close-headline').getByTestId('emphasis')).toHaveCount(0);
  await expect(page.getByTestId('close-sub').getByTestId('emphasis')).toHaveText('learned');
});

test('a slide without emphasis draws the bare text', async ({ mount, page }) => {
  await mount(render(slideOf(base)));
  await expect(page.getByTestId('emphasis')).toHaveCount(0);
  await expect(page.getByTestId('close-line').first().locator('*')).toHaveCount(0);
});

// Capitals and wide letters of the same lengths.
const WIDE = 'Many Women Who Wander Wide Meadows Mow Moss With Me When Warm, Whom We Name WOMBAT ';
function wide(length: number): string {
  let text = '';
  while (text.length < length) text += WIDE;
  return text.slice(0, length).trimEnd().padEnd(length, 'M');
}

test('an emphasised wide close slide stays inside the box', async ({ mount, page }) => {
  await stubApi(page, {});
  const lines = [wide(22), wide(22), wide(22)];
  const sub = wide(110);
  await mount(
    render(
      slideOf({
        headline: lines.join('\n'),
        sub,
        author: wide(60),
        emphasis: [
          { text: lines[1].slice(0, 10), tone: 'accent' },
          { text: sub.slice(0, 12), tone: 'negative', in: 'sub' },
        ],
      }),
    ),
  );
  await page.evaluate(() => document.fonts.ready);
  const drawn = await page.getByTestId('layout-close').evaluate((root: Element) => {
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
  expect(drawn.left).toBeGreaterThanOrEqual(120);
  expect(drawn.right).toBeLessThanOrEqual(1800);
  expect(drawn.bottom).toBeLessThanOrEqual(996);
});

test('screenshot: close emphasis', async ({ mount }) => {
  const component = await mount(
    render(
      slideOf({
        headline: 'Pick one story.\nShip half of it by Friday.',
        sub: 'Then tell us what you learned at the next demo.',
        author: 'Sam Rivera · Product Engineering',
        emphasis: [
          { text: 'half of it', tone: 'accent' },
          { text: 'learned', tone: 'positive', in: 'sub' },
        ],
      }),
    ),
  );
  await expect(component).toHaveScreenshot('layout-close-emphasis.png');
});
