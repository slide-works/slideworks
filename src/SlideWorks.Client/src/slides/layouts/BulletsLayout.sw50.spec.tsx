import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { BulletsSlots } from '../slots/bullets';
import type { SlideDeck } from '../slots';

// The bullets layout's SW-50 additions: an icon on a point, emphasis in the
// headline, a longer lead, a callout that arrives last and a dark surface.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function bullets(slots: BulletsSlots, surface?: 'light' | 'dark', number = 3): RenderableSlide {
  return { number, layout: 'bullets', part: null, deepDive: false, surface, slots };
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

test("icon in the mark's place", async ({ mount, page }) => {
  await mount(
    render(
      bullets({
        headline: 'Icons',
        items: [{ icon: 'bolt', text: 'Fast' }, 'Plain'],
      }),
    ),
  );
  const items = page.getByTestId('bullets-body').locator('li');
  const li = items.first();
  const icon = page.getByTestId('bullets-icon');
  await expect(icon).toHaveCount(1);
  await expect(page.getByTestId('block-icon')).toHaveAttribute('data-icon', 'bolt');
  const liBox = (await li.boundingBox())!;
  const iconBox = (await icon.boundingBox())!;
  expect(iconBox.x).toBeCloseTo(liBox.x, 0);
  expect(iconBox.width).toBeCloseTo(40, 0);
  const text = (await page.getByTestId('bullets-item').first().boundingBox())!;
  expect(text.x - liBox.x).toBeCloseTo(68, 0);
  expect(await li.evaluate((el) => getComputedStyle(el, '::before').content)).toBe('none');
  // The point without an icon keeps its square mark and the same indent.
  const plain = items.nth(1);
  expect(await plain.evaluate((el) => getComputedStyle(el, '::before').content)).not.toBe('none');
});

test('an unknown icon name draws no icon and keeps the square mark', async ({ mount, page }) => {
  await mount(
    render(
      bullets({
        headline: 'Unknown',
        items: [{ icon: 'no-such-icon', text: 'Mystery' }, 'Plain'],
      }),
    ),
  );
  await expect(page.getByTestId('bullets-icon')).toHaveCount(0);
  await expect(page.getByTestId('block-icon')).toHaveCount(0);
  const items = page.getByTestId('bullets-body').locator('li');
  for (const li of [items.first(), items.nth(1)]) {
    expect(await li.evaluate((el) => getComputedStyle(el, '::before').content)).not.toBe('none');
  }
  const unknown = (await page.getByTestId('bullets-item').first().boundingBox())!;
  const plain = (await page.getByTestId('bullets-item').nth(1).boundingBox())!;
  expect(unknown.x).toBeCloseTo(plain.x, 0);
});

test('dark bullets use night ink', async ({ mount, page }) => {
  await mount(render(bullets({ headline: 'Night', items: ['Plain point'] }, 'dark')));
  const colour = await page
    .getByTestId('bullets-item')
    .first()
    .evaluate((el) => getComputedStyle(el).color);
  expect(colour).toBe(await tokenColour(page, 'bullets-item', 'night-ink'));
});

test('callout arrives on the step after the last item', async ({ mount, page }) => {
  const slide = bullets({
    headline: 'Last',
    items: ['a', 'b', 'c'],
    reveal: 'one-by-one',
    callout: { tone: 'info', text: 'Closing line', reveal: 'last' },
  });
  const component = await mount(render(slide, 3));
  const callout = page.getByTestId('body-callout').locator('.sw-reveal');
  await expect(callout).toHaveAttribute('data-revealed', 'false');
  await component.update(render(slide, 4));
  await expect(callout).toHaveAttribute('data-revealed', 'true');
});

test('an item with only text draws no lead', async ({ mount, page }) => {
  await mount(render(bullets({ headline: 'Text', items: [{ text: 'Just text' }] })));
  await expect(page.getByTestId('bullets-item')).toHaveText('Just text');
  await expect(page.getByTestId('bullets-item').locator('b')).toHaveCount(0);
});

test('headline emphasis carries its tone', async ({ mount, page }) => {
  await mount(
    render(
      bullets({
        headline: 'Small steps beat big plans',
        items: ['a'],
        emphasis: [{ text: 'big plans', tone: 'negative' }],
      }),
    ),
  );
  const phrase = page.getByTestId('emphasis');
  await expect(phrase).toHaveText('big plans');
  await expect(phrase).toHaveAttribute('data-tone', 'negative');
});

test('bulletsIcons and its wide twin stay inside the box', async ({ mount, page }) => {
  await stubApi(page, {});
  const fixture = fitFixture('bulletsIcons');
  const original = fixture.slots as BulletsSlots;
  const twin = widen(original) as BulletsSlots;
  for (const [name, slots] of [
    ['fixture', original],
    ['wide twin', twin],
  ] as const) {
    const component = await mount(render(bullets(slots), slots.items.length));
    await page.evaluate(() => document.fonts.ready);
    const box = await page.getByTestId('layout-bullets').evaluate((root: Element) => {
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
    expect(box.left).toBeGreaterThanOrEqual(120);
    expect(box.right).toBeLessThanOrEqual(1800);
    if (name === 'fixture') expect(box.bottom).toBeLessThanOrEqual(996);
    await component.unmount();
  }
});

test('screenshot: bullets with icons', async ({ mount }) => {
  const component = await mount(
    render(
      bullets({
        kicker: 'The cost',
        headline: 'Every slip we had last year started as a big batch',
        items: [
          { icon: 'bolt', lead: 'Risk shows up late.', text: 'Nobody sees it coming.' },
          { icon: 'clock', text: 'Merges get scary.' },
          { icon: 'check', text: 'Small slices ship fast.' },
        ],
        emphasis: [{ text: 'big batch', tone: 'negative' }],
      }),
    ),
  );
  await expect(component).toHaveScreenshot('layout-bullets-icons.png');
});

test('screenshot: bullets dark', async ({ mount }) => {
  const component = await mount(
    render(
      bullets(
        {
          kicker: 'The cost',
          headline: 'Every slip we had last year started as a big batch',
          items: ['Risk shows up late.', { lead: 'Merges get scary.', text: 'Branches collide.' }],
          callout: { tone: 'info', text: 'Nobody can say no to a slice.' },
        },
        'dark',
      ),
    ),
  );
  await expect(component).toHaveScreenshot('layout-bullets-dark.png');
});
