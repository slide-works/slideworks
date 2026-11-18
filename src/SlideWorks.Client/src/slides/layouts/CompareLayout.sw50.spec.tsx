import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixturesOf } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { CompareSlots } from '../slots/compare';
import type { SlideDeck } from '../slots';

// The compare layout's SW-50 additions: label-and-value rows, one large line,
// an inline stamp, a footer, a plain fill, an icon, a callout, headline
// emphasis and the two steps.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function compareSlide(slots: CompareSlots, number = 3): RenderableSlide {
  return { number, layout: 'compare', part: 1, deepDive: false, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

// Wide letters (capitals, W and M) of exactly `length` characters.
const WIDE_TEXT =
  'Many Women Who Wander Wide Meadows Mow Moss With Me When Warm, ' +
  'Whom We Name WOMBAT MUMMY Mavens, Wow, Momentum Wins Most Wednesdays. ';
function wide(length: number): string {
  let text = '';
  while (text.length < length) text += WIDE_TEXT;
  return text.slice(0, length).trimEnd().padEnd(length, 'M');
}

// Keys whose strings are not text: they stay as they are.
const NOT_TEXT = new Set(['tone', 'fill', 'icon', 'reveal', 'in']);

function widen(value: unknown, key?: string): unknown {
  if (typeof value === 'string')
    return key !== undefined && NOT_TEXT.has(key) ? value : wide(value.length);
  if (Array.isArray(value)) return value.map((item) => widen(item, key));
  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, widen(v, k)]));
  }
  return value;
}

const box = (page: Page, testId: string, index = 0) =>
  page.getByTestId(testId).nth(index).boundingBox();

// The panels' drawn text extents, and each panel's top and bottom edges, in
// canvas px from the slide's top left.
function measure(page: Page) {
  return page.getByTestId('layout-compare').evaluate((root: Element) => {
    const slide = root.closest('[data-layout]')!.getBoundingClientRect();
    let left = Infinity;
    let right = -Infinity;
    let bottom = -Infinity;
    let top = Infinity;
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
      const range = document.createRange();
      range.selectNodeContents(node);
      for (const rect of range.getClientRects()) {
        if (rect.width === 0) continue;
        left = Math.min(left, rect.left - slide.left);
        right = Math.max(right, rect.right - slide.left);
        top = Math.min(top, rect.top - slide.top);
        bottom = Math.max(bottom, rect.bottom - slide.top);
      }
    }
    const panels = [...root.querySelectorAll('section')].map((panel) => {
      const rect = panel.getBoundingClientRect();
      return { top: rect.top - slide.top, bottom: rect.bottom - slide.top, height: rect.height };
    });
    // The largest overshoot of a panel's text past its panel's bottom.
    let overshoot = -Infinity;
    for (const panel of root.querySelectorAll('section')) {
      const limit = panel.getBoundingClientRect().bottom;
      const inner = document.createTreeWalker(panel, NodeFilter.SHOW_TEXT);
      for (let node = inner.nextNode(); node !== null; node = inner.nextNode()) {
        const range = document.createRange();
        range.selectNodeContents(node);
        for (const rect of range.getClientRects()) {
          if (rect.width > 0) overshoot = Math.max(overshoot, rect.bottom - limit);
        }
      }
    }
    return { left, right, top, bottom, panels, overshoot };
  });
}

const BASE = {
  label: 'Checkout',
  title: 'Sliced by layer',
  points: ['One point.'],
};
const failure = (more: Partial<CompareSlots['failure']> = {}): CompareSlots['failure'] => ({
  ...BASE,
  ...more,
});
const success = (more: Partial<CompareSlots['success']> = {}): CompareSlots['success'] => ({
  ...BASE,
  label: 'Search',
  ...more,
});
const ROWS = [
  { label: 'Releases', value: 'Nine' },
  { label: 'Lead time', value: 'Two days' },
];

test('rows panel', async ({ mount, page }) => {
  await mount(
    render(
      compareSlide({
        headline: 'Rows',
        failure: failure({ points: undefined, rows: ROWS }),
        success: success(),
      }),
    ),
  );
  const rows = page.getByTestId('compare-failure').getByTestId('compare-row');
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(0).getByTestId('compare-row-label')).toHaveText('Releases');
  await expect(rows.nth(1).getByTestId('compare-row-value')).toHaveText('Two days');
  const first = (await rows.nth(0).boundingBox())!;
  const second = (await rows.nth(1).boundingBox())!;
  const label = (await rows.nth(0).getByTestId('compare-row-label').boundingBox())!;
  const value = (await rows.nth(0).getByTestId('compare-row-value').boundingBox())!;
  expect(label.width).toBeCloseTo(180, 0);
  expect(value.x).toBeGreaterThanOrEqual(label.x + 180);
  // 12 px above and below, a 1 px line on top, one 38.4 px value line: 63.4 px.
  expect(first.height).toBeCloseTo(63.4, 0);
  expect(second.y - first.y).toBeCloseTo(first.height, 0);
  const css = await rows.nth(0).evaluate((e) => getComputedStyle(e).borderTopWidth);
  expect(css).toBe('1px');
  const labelStyle = await rows
    .nth(0)
    .getByTestId('compare-row-label')
    .evaluate((e) => {
      const s = getComputedStyle(e);
      return [s.fontSize, s.fontWeight, s.textTransform, s.overflowWrap];
    });
  expect(labelStyle).toEqual(['16px', '700', 'uppercase', 'anywhere']);
  const valueStyle = await rows
    .nth(0)
    .getByTestId('compare-row-value')
    .evaluate((e) => {
      const s = getComputedStyle(e);
      return [s.fontSize, s.fontWeight];
    });
  expect(valueStyle).toEqual(['32px', '700']);
});

test('a long row label wraps inside its column', async ({ mount, page }) => {
  await mount(
    render(
      compareSlide({
        headline: 'Labels',
        failure: failure({ points: undefined, rows: [{ label: wide(16), value: 'Value' }] }),
        success: success(),
      }),
    ),
  );
  const label = (await box(page, 'compare-row-label'))!;
  const value = (await box(page, 'compare-row-value'))!;
  expect(label.width).toBeCloseTo(180, 0);
  expect(label.x + label.width).toBeLessThanOrEqual(value.x);
});

test('line panel with emphasis', async ({ mount, page }) => {
  await mount(
    render(
      compareSlide({
        headline: 'Line',
        failure: failure({ points: undefined, line: '"Ship it when it is ready."' }),
        success: success({ points: undefined, line: '"Ship it every week."' }),
        emphasis: [{ text: 'every week', tone: 'positive', in: 'success.line' }],
      }),
    ),
  );
  const line = page.getByTestId('compare-success').getByTestId('compare-line');
  await expect(line).toHaveText('"Ship it every week."');
  const style = await line.evaluate((e) => {
    const s = getComputedStyle(e);
    return [s.fontSize, s.fontWeight, s.lineHeight];
  });
  expect(style).toEqual(['40px', '700', '50px']);
  const phrase = line.getByTestId('emphasis');
  await expect(phrase).toHaveText('every week');
  await expect(phrase).toHaveAttribute('data-tone', 'positive');
  await expect(
    page.getByTestId('compare-failure').getByTestId('compare-line').getByTestId('emphasis'),
  ).toHaveCount(0);
});

test('stamps land together', async ({ mount, page }) => {
  const slots: CompareSlots = {
    headline: 'Stamps',
    failure: failure({ stamp: 'Late' }),
    success: success({ stamp: 'Live' }),
    reveal: 'one-by-one',
  };
  const component = await mount(render(compareSlide(slots), 1));
  await page.evaluate(() => document.fonts.ready);
  const stamps = page.getByTestId('block-stamp');
  await expect(stamps).toHaveCount(2);
  const reveals = stamps.locator('xpath=..');
  await expect(reveals).toHaveCount(2);
  for (const i of [0, 1]) await expect(reveals.nth(i)).toHaveAttribute('data-revealed', 'false');
  const before = await Promise.all([0, 1].map((i) => stamps.nth(i).boundingBox()));
  await component.update(render(compareSlide(slots), 2));
  for (const i of [0, 1]) await expect(reveals.nth(i)).toHaveAttribute('data-revealed', 'true');
  expect(await Promise.all([0, 1].map((i) => stamps.nth(i).boundingBox()))).toEqual(before);
  const style = await stamps.first().evaluate((e) => {
    const s = getComputedStyle(e);
    return [s.borderTopWidth, s.fontSize, s.fontWeight, s.textTransform, s.position];
  });
  expect(style).toEqual(['4px', '26px', '800', 'uppercase', 'relative']);
  // 24 px above, 54 px tall (26 + 2 x 10 padding + 2 x 4 border): 78 px in flow.
  const panel = page.getByTestId('compare-failure');
  const slot = (await panel.getByTestId('block-stamp').locator('xpath=..').boundingBox())!;
  expect(slot.height).toBeCloseTo(78, 0);
});

test('success panel hidden keeps its space', async ({ mount, page }) => {
  const slots: CompareSlots = {
    headline: 'Steps',
    failure: failure(),
    success: success(),
    reveal: 'one-by-one',
  };
  const component = await mount(render(compareSlide(slots), 0));
  const hidden = (await box(page, 'compare-success'))!;
  const reveal = page.getByTestId('compare-body').locator('> .sw-reveal');
  await expect(reveal).toHaveAttribute('data-revealed', 'false');
  expect(hidden.width).toBeCloseTo(820, 0);
  await component.update(render(compareSlide(slots), 1));
  await expect(reveal).toHaveAttribute('data-revealed', 'true');
  expect((await box(page, 'compare-success'))!).toEqual(hidden);
});

test('without reveal the panels show from step 0', async ({ mount, page }) => {
  await mount(
    render(
      compareSlide({
        headline: 'All',
        failure: failure({ stamp: 'Late' }),
        success: success({ stamp: 'Live' }),
      }),
      0,
    ),
  );
  await expect(page.locator('.sw-reveal[data-revealed="false"]')).toHaveCount(0);
});

test('footer sits at the bottom of the shorter panel', async ({ mount, page }) => {
  await mount(
    render(
      compareSlide({
        headline: 'Footer',
        failure: failure({ footer: 'Short footer.' }),
        success: success({
          points: ['A longer point that goes on. '.repeat(3), 'Another. '.repeat(6)],
          footer: 'Short footer.',
        }),
      }),
    ),
  );
  const left = (await box(page, 'compare-failure'))!;
  const right = (await box(page, 'compare-success'))!;
  expect(left.height).toBeCloseTo(right.height, 0);
  const footer = (await box(page, 'compare-footer', 0))!;
  // 44 px of padding under the footer's last line.
  expect(left.y + left.height - (footer.y + footer.height)).toBeCloseTo(44, 0);
  const other = (await box(page, 'compare-footer', 1))!;
  expect(other.y + other.height).toBeCloseTo(footer.y + footer.height, 0);
  const style = await page
    .getByTestId('compare-footer')
    .first()
    .evaluate((e) => {
      const s = getComputedStyle(e);
      return [s.fontSize, s.fontWeight, s.borderTopWidth, s.paddingTop];
    });
  expect(style).toEqual(['24px', '400', '1px', '14px']);
});

test('plain and solid fills', async ({ mount, page }) => {
  const component = await mount(
    render(
      compareSlide({
        headline: 'Fills',
        failure: failure({ tone: 'positive', fill: 'plain', stamp: 'Late' }),
        success: success({ tone: 'negative', fill: 'solid', stamp: 'Live' }),
      }),
    ),
  );
  const colours = await page.evaluate(() => {
    const colour = (value: string) => {
      const probe = document.createElement('div');
      probe.style.color = value;
      document.body.append(probe);
      const result = getComputedStyle(probe).color;
      probe.remove();
      return result;
    };
    const plain = document.querySelector('[data-testid="compare-failure"]')!;
    const solid = document.querySelector('[data-testid="compare-success"]')!;
    const stamp = (panel: Element) =>
      getComputedStyle(panel.querySelector('[data-testid="block-stamp"]')!).color;
    return {
      card: colour('var(--sw-card)'),
      ink: colour('var(--sw-ink)'),
      good: colour('var(--sw-good)'),
      goodInk: colour('var(--sw-good-ink)'),
      solidInk: colour('var(--sw-panel-negative-ink)'),
      plainBackground: getComputedStyle(plain).backgroundColor,
      plainEdge: getComputedStyle(plain).borderTopColor,
      plainEdgeWidth: getComputedStyle(plain).borderTopWidth,
      plainTitle: getComputedStyle(plain.querySelector('[data-testid="compare-title"]')!).color,
      plainTag: getComputedStyle(plain.querySelector('[data-testid="compare-label"]')!).color,
      plainStamp: stamp(plain),
      solidStamp: stamp(solid),
    };
  });
  expect(colours.plainBackground).toBe(colours.card);
  expect(colours.plainEdge).toBe(colours.good);
  expect(colours.plainEdgeWidth).toBe('10px');
  expect(colours.plainTitle).toBe(colours.ink);
  expect(colours.plainTag).toBe(colours.goodInk);
  expect(colours.plainStamp).toBe(colours.good);
  expect(colours.solidStamp).toBe(colours.solidInk);

  await component.update(
    render(
      compareSlide({
        headline: 'Inverse',
        failure: failure({ tone: 'inverse', fill: 'plain' }),
        success: success(),
      }),
    ),
  );
  const inverse = await page.evaluate(() => {
    const probe = document.createElement('div');
    probe.style.color = 'var(--sw-ink)';
    document.body.append(probe);
    const ink = getComputedStyle(probe).color;
    probe.remove();
    const panel = document.querySelector('[data-testid="compare-failure"]')!;
    return {
      ink,
      tag: getComputedStyle(panel.querySelector('[data-testid="compare-label"]')!).color,
    };
  });
  expect(inverse.tag).toBe(inverse.ink);
});

test('icon beside the label', async ({ mount, page }) => {
  await mount(
    render(
      compareSlide({
        headline: 'Icons',
        failure: failure({ icon: 'coins' }),
        success: success({ icon: 'not-an-icon' }),
      }),
    ),
  );
  await expect(page.getByTestId('block-icon')).toHaveCount(1);
  const icon = (await box(page, 'block-icon'))!;
  const label = (await box(page, 'compare-label', 0))!;
  expect(icon.x + icon.width).toBeLessThanOrEqual(label.x);
  expect(icon.height).toBeLessThanOrEqual(18);
  // The tag row is no taller with the icon: the titles of both panels start at the same y.
  const one = (await box(page, 'compare-title', 0))!;
  const two = (await box(page, 'compare-title', 1))!;
  expect(one.y).toBeCloseTo(two.y, 0);
});

test('callout under the panels', async ({ mount, page }) => {
  const slots: CompareSlots = {
    headline: 'Callout',
    failure: failure({ stamp: 'Late' }),
    success: success({ stamp: 'Live' }),
    reveal: 'one-by-one',
    callout: { tone: 'info', text: 'Last', reveal: 'last' },
  };
  const component = await mount(render(compareSlide(slots), 2));
  const callout = (await box(page, 'body-callout'))!;
  const panel = (await box(page, 'compare-failure'))!;
  expect(panel.y + panel.height).toBeLessThanOrEqual(callout.y);
  expect(callout.y + callout.height).toBeLessThanOrEqual(996);
  const reveal = page.getByTestId('body-callout').locator('.sw-reveal');
  await expect(reveal).toHaveAttribute('data-revealed', 'false');
  await component.update(render(compareSlide(slots), 3));
  await expect(reveal).toHaveAttribute('data-revealed', 'true');
  expect((await box(page, 'body-callout'))!).toEqual(callout);
});

test('headline emphasis carries its tone', async ({ mount, page }) => {
  await mount(
    render(
      compareSlide({
        headline: 'Everyone changes one habit',
        failure: failure(),
        success: success(),
        emphasis: [{ text: 'one habit', tone: 'positive', in: 'headline' }],
      }),
    ),
  );
  const phrase = page.getByRole('heading', { level: 2 }).getByTestId('emphasis');
  await expect(phrase).toHaveText('one habit');
  await expect(phrase).toHaveAttribute('data-tone', 'positive');
});

// Until the server's fixtures reach this branch they are proved with inline
// slots: the widest rows panel, a points panel, a line and a footer.
const WIDE_ROWS = [0, 1, 2, 3].map(() => ({
  label: 'Make Small And',
  value: 'Make Small Momentum Software',
}));
const INLINE: Record<string, CompareSlots> = {
  inlineRows: {
    headline: 'Rows at the limit',
    failure: {
      label: 'Every And More Faster',
      title: 'Make Small Momentum Software',
      rows: WIDE_ROWS,
      stamp: 'Merge Momentum',
      footer: 'Small Momentum Than Ship Week Faster See And Waits',
    },
    success: {
      label: 'Every And More Faster',
      title: 'Make Small Momentum Software',
      points: [0, 1, 2, 3].map(() =>
        'When Customers See Real Progress Weekly Metrics Show Where Work Waits Make Every Real'.padEnd(
          90,
          'M',
        ),
      ),
    },
  },
  inlineLine: {
    headline: 'A line at the limit',
    failure: {
      label: 'Every And More Faster',
      title: 'Make Small Momentum Than Ship Week Faster Make And',
      sub: 'Every And Matters Maximum Working Faster More',
      line: 'Momentum Than Ship Week Faster See Metrics Work Every And Matters Maximum Working Teams Progress See',
      footer:
        'Merge Reviewable More Throughput Software Learn Customers Progress Where Make Momentum See',
    },
    success: {
      label: 'Every And More Faster',
      title: 'Make Small Momentum Than Ship Week Faster Make And',
      line: 'Momentum Than Ship Week Faster See Metrics Work Every And Matters Maximum Working Teams Progress See',
    },
  },
};

const FIT: Record<string, CompareSlots> = { ...INLINE };
for (const [name, fixture] of Object.entries(fitFixturesOf('compare'))) {
  if (name !== 'compare') FIT[name] = fixture.slots as CompareSlots;
}

for (const [name, slots] of Object.entries(FIT)) {
  test(`${name} stays inside the box`, async ({ mount, page }) => {
    await stubApi(page, {});
    await mount(render(compareSlide(slots, 5)));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await measure(page);
    console.log(
      `${name}: drawn bottom ${drawn.bottom}, panels ${drawn.panels.map((p) => p.height.toFixed(1)).join('/')}, estimate ${fitFixturesOf('compare')[name]?.estimate ?? 'n/a'}`,
    );
    expect(drawn.left, name).toBeGreaterThanOrEqual(120);
    expect(drawn.right, name).toBeLessThanOrEqual(1800);
    expect(Math.min(...drawn.panels.map((p) => p.top)), name).toBeGreaterThanOrEqual(150);
    expect(drawn.bottom, name).toBeLessThanOrEqual(996);
    expect(drawn.overshoot, name).toBeLessThanOrEqual(0.5);
  });

  test(`${name} with wide letters stays inside x 120-1800`, async ({ mount, page }) => {
    await stubApi(page, {});
    await mount(render(compareSlide(widen(slots) as CompareSlots, 5)));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await measure(page);
    console.log(
      `${name} wide: drawn bottom ${drawn.bottom}, panels ${drawn.panels.map((p) => p.height.toFixed(1)).join('/')}, overshoot ${drawn.overshoot}`,
    );
    expect(drawn.left, name).toBeGreaterThanOrEqual(120);
    expect(drawn.right, name).toBeLessThanOrEqual(1800);
  });
}

test('screenshot: compare rows', async ({ mount }) => {
  const component = await mount(
    render(
      compareSlide({
        kicker: 'Two teams, one quarter',
        headline: 'What each team had to show',
        failure: failure({
          points: undefined,
          rows: [
            { label: 'Releases', value: 'One' },
            { label: 'Lead time', value: 'Eleven weeks' },
            { label: 'Feedback', value: 'At the end' },
          ],
          footer: 'Nothing to show for 11 weeks.',
        }),
        success: success({
          points: undefined,
          rows: [
            { label: 'Releases', value: 'Nine' },
            { label: 'Lead time', value: 'Two days' },
            { label: 'Feedback', value: 'Every week' },
          ],
          footer: 'Something live every week.',
        }),
      }),
    ),
  );
  await expect(component).toHaveScreenshot('layout-compare-rows.png');
});

test('screenshot: compare line', async ({ mount }) => {
  const component = await mount(
    render(
      compareSlide({
        headline: 'What the customer said',
        failure: failure({
          points: undefined,
          line: '"We waited eleven weeks to see anything."',
          icon: 'coins',
        }),
        success: success({
          points: undefined,
          line: '"Every week there was something to try."',
          icon: 'coins',
        }),
        emphasis: [{ text: 'something to try', tone: 'positive', in: 'success.line' }],
      }),
    ),
  );
  await expect(component).toHaveScreenshot('layout-compare-line.png');
});

test('screenshot: compare stamps', async ({ mount }) => {
  const component = await mount(
    render(
      compareSlide(
        {
          headline: 'The verdict',
          failure: failure({ stamp: 'Late', fill: 'plain' }),
          success: success({ stamp: 'On time', fill: 'solid', tone: 'positive' }),
          reveal: 'one-by-one',
        },
        3,
      ),
      2,
    ),
  );
  await expect(component).toHaveScreenshot('layout-compare-stamps.png');
});
