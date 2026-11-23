import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { SAMPLE_IMAGE_ID } from '../../test-support/fixtures/layoutSlots';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { ChartSlots, ImageSlots, SlideDeck } from '../slots';
import { DATA_FIT_FIXTURES, wideTwin, type DataFitFixture } from './DataLayouts.fixtures';

// The image, table, chart and gantt layouts, rendered through SlideRenderer
// inside the real frame.

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

// The sample deck has no image slide (SW-52), so this one is the tests' own.
const IMAGE_SLOTS: ImageSlots = {
  kicker: 'The tool',
  headline: 'A story map shows the thinnest release',
  image: { image: SAMPLE_IMAGE_ID, alt: 'A story map: four customer steps across, releases down' },
  points: ['One row is one release.', 'The top row covers every step, done simply.'],
  caption: 'story-map.png, uploaded by the agent',
};

function imageSlide(slots: ImageSlots): RenderableSlide {
  return { ...sample(10), layout: 'image', slots } as RenderableSlide;
}

function chartSlide(slots: ChartSlots): RenderableSlide {
  return { ...sample(12), layout: 'chart', slots } as RenderableSlide;
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={0} />;
}

const GOOD = 'rgb(21, 128, 61)';
const BAD = 'rgb(220, 38, 38)';
const ACCENT = 'rgb(47, 91, 234)';
const WARN = 'rgb(234, 88, 12)';
const WHITE = 'rgb(255, 255, 255)';

type Box = { left: number; top: number; right: number; bottom: number };

// What the layout draws, in canvas px from the slide's top left: every run of
// text, every painted box (an image, or a background, shadow or border) and
// every SVG shape or text inside the layout. Boxes that only stretch to fill
// the body are left out. Also the top of every element box, the body's box,
// the headline's box and the footer's top.
async function measure(page: Page, layoutTestId: string) {
  await page.evaluate(() => document.fonts.ready);
  return page.getByTestId(layoutTestId).evaluate((root: Element) => {
    const slide = root.closest('[data-layout]')!.getBoundingClientRect();
    const rel = (rect: DOMRect): Box => ({
      left: rect.left - slide.left,
      top: rect.top - slide.top,
      right: rect.right - slide.left,
      bottom: rect.bottom - slide.top,
    });
    const drawn = { left: Infinity, top: Infinity, right: -Infinity, bottom: -Infinity };
    const add = (rect: DOMRect) => {
      if (rect.width === 0 && rect.height === 0) return;
      const box = rel(rect);
      drawn.left = Math.min(drawn.left, box.left);
      drawn.top = Math.min(drawn.top, box.top);
      drawn.right = Math.max(drawn.right, box.right);
      drawn.bottom = Math.max(drawn.bottom, box.bottom);
    };
    let boxTop = Infinity;
    for (const element of root.querySelectorAll('*')) {
      const rect = element.getBoundingClientRect();
      const svg = element instanceof SVGElement;
      if (!svg && (rect.width > 0 || rect.height > 0)) boxTop = Math.min(boxTop, rel(rect).top);
      if (svg) {
        if (element.tagName !== 'svg' && element.tagName !== 'g') add(rect);
        continue;
      }
      const style = getComputedStyle(element);
      const painted =
        element.tagName === 'IMG' ||
        style.backgroundColor !== 'rgba(0, 0, 0, 0)' ||
        style.backgroundImage !== 'none' ||
        style.boxShadow !== 'none' ||
        parseFloat(style.borderTopWidth) > 0 ||
        parseFloat(style.borderBottomWidth) > 0;
      if (painted) add(rect);
    }
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
      const range = document.createRange();
      range.selectNodeContents(node);
      for (const rect of range.getClientRects()) add(rect);
    }
    const body = rel(root.querySelector('.sw-body')!.getBoundingClientRect());
    const headline = rel(root.querySelector('h2')!.getBoundingClientRect());
    const footer = document.querySelector('[data-testid="slide-footer"]')!.getBoundingClientRect();
    return { drawn, boxTop, body, headline, footerTop: footer.top - slide.top };
  });
}

// An SVG image of the given natural size, served for /api/images/<id>.
async function stubImage(page: Page, id: string, width: number, height: number) {
  await page.route(`**/api/images/${id}`, (route) =>
    route.fulfill({
      contentType: 'image/svg+xml',
      body: `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}"><rect width="${width}" height="${height}" fill="#2563eb"/></svg>`,
    }),
  );
}

async function loaded(page: Page, testId: string) {
  const image = page.getByTestId(testId);
  await expect
    .poll(() => image.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth))
    .toBeGreaterThan(0);
  return image;
}

test('image renders with alt from slot', async ({ mount, page }) => {
  await stubApi(page, {});
  await mount(render(imageSlide(IMAGE_SLOTS)));

  const layout = page.getByTestId('layout-image');
  await expect(layout.getByTestId('image-kicker')).toHaveText('The tool');
  await expect(layout.getByRole('heading', { level: 2 })).toHaveText(IMAGE_SLOTS.headline);
  const image = await loaded(page, 'image-picture');
  await expect(image).toHaveAttribute('src', `/api/images/${SAMPLE_IMAGE_ID}`);
  await expect(image).toHaveAttribute('alt', IMAGE_SLOTS.image.alt);
  await expect(layout.getByRole('img', { name: IMAGE_SLOTS.image.alt })).toBeVisible();
  await expect(image).toHaveCSS('object-fit', 'contain');
  await expect(layout.getByTestId('image-point')).toHaveText(
    IMAGE_SLOTS.points!.map((p) => (typeof p === 'string' ? p : p.text)),
  );
  await expect(layout.getByTestId('image-caption')).toHaveText(IMAGE_SLOTS.caption!);
  const box = (await image.boundingBox())!;
  const points = (await layout.getByTestId('image-point').first().boundingBox())!;
  expect(box.x + box.width).toBeLessThan(points.x);
});

test('image without points or caption takes the body width', async ({ mount, page }) => {
  await stubImage(page, 'img_wide', 3200, 1000);
  await mount(
    render(imageSlide({ headline: 'Wide', image: { image: 'img_wide', alt: 'A wide picture' } })),
  );
  const image = await loaded(page, 'image-picture');
  await expect(page.getByTestId('image-point')).toHaveCount(0);
  await expect(page.getByTestId('image-caption')).toHaveCount(0);
  await expect.poll(async () => Math.round((await image.boundingBox())!.width)).toBe(1680);
  const box = (await image.boundingBox())!;
  // Scaled whole: the picture keeps its 3.2:1 ratio, nothing cropped.
  expect(box.width / box.height).toBeCloseTo(3.2, 1);
});

test('image grows to at most twice its size, centred', async ({ mount, page }) => {
  await stubImage(page, 'img_small', 120, 80);
  await mount(
    render(
      imageSlide({ headline: 'Small', image: { image: 'img_small', alt: 'A small picture' } }),
    ),
  );
  const image = await loaded(page, 'image-picture');
  await expect(image).toHaveCSS('background-color', WHITE);
  await expect.poll(async () => (await image.boundingBox())?.width).toBe(240);
  const box = (await image.boundingBox())!;
  const body = (await page.getByTestId('image-body').boundingBox())!;
  expect(box.height).toBeCloseTo(160, 0);
  expect(box.x - body.x).toBeCloseTo((body.width - 240) / 2, 0);
});

test('image fits a tall picture to the height above its caption, uncropped', async ({
  mount,
  page,
}) => {
  await stubImage(page, 'img_tall', 400, 2000);
  await mount(
    render(
      imageSlide({
        headline: 'Tall',
        image: { image: 'img_tall', alt: 'A tall picture' },
        caption: 'Under it',
        points: ['Beside it'],
      }),
    ),
  );
  const image = await loaded(page, 'image-picture');
  await expect
    .poll(async () => {
      const box = (await image.boundingBox())!;
      return Math.round(box.width * 5 - box.height);
    })
    .toBe(0);
  const box = (await image.boundingBox())!;
  const caption = (await page.getByTestId('image-caption').boundingBox())!;
  expect(box.y + box.height).toBeLessThanOrEqual(caption.y);
  const { drawn } = await measure(page, 'layout-image');
  expect(drawn.bottom).toBeLessThanOrEqual(996);
});

test('table tones and highlighted row', async ({ mount, page }) => {
  await mount(render(sample(11)));

  const layout = page.getByTestId('layout-table');
  await expect(layout.getByTestId('table-kicker')).toHaveText('Sizing');
  await expect(layout.getByRole('heading', { level: 2 })).toHaveText(
    'Aim for slices a pair can ship inside a week',
  );
  await expect(layout.getByRole('columnheader')).toHaveText([
    'Slice size',
    'Review time',
    'Rollback',
    'Customer feedback',
    'Verdict',
  ]);
  const rows = layout.getByTestId('table-row');
  await expect(rows).toHaveCount(4);
  await expect(rows.nth(0).getByTestId('table-cell')).toHaveText([
    'One quarter',
    'Days',
    'Painful',
    'Week 10+',
    'Avoid',
  ]);

  const badges = layout.getByTestId('block-badge');
  await expect(badges).toHaveText(['Avoid', 'OK', 'Target', 'Great']);
  const tones = [
    ['bad', 'rgb(153, 27, 27)'],
    ['warn', 'rgb(154, 52, 18)'],
    ['good', 'rgb(22, 101, 52)'],
    ['info', 'rgb(30, 58, 138)'],
  ];
  for (const [i, [tone, color]] of tones.entries()) {
    await expect(badges.nth(i)).toHaveAttribute('data-tone', tone);
    await expect(badges.nth(i)).toHaveCSS('color', color);
  }

  // highlightRow 3 is 1-based: the third row, "2–4 days", gets the band.
  await expect(layout.locator('tr[data-highlight]')).toHaveCount(1);
  await expect(rows.nth(2)).toHaveAttribute('data-highlight', 'true');
  await expect(rows.nth(2).getByTestId('table-cell').first()).toHaveText('2–4 days');
  await expect(rows.nth(2).getByTestId('table-cell').first()).toHaveCSS(
    'box-shadow',
    `${ACCENT} 6px 0px 0px 0px inset`,
  );
  await expect(rows.nth(2).getByTestId('table-cell').nth(1)).not.toHaveCSS(
    'background-color',
    'rgba(0, 0, 0, 0)',
  );
  await expect(rows.nth(1).getByTestId('table-cell').nth(1)).toHaveCSS(
    'background-color',
    'rgba(0, 0, 0, 0)',
  );
});

test('a card badge with no tone keeps the accent', async ({ mount, page }) => {
  await mount(render(sample(15)));
  const badge = page.getByTestId('block-badge').first();
  await expect(badge).not.toHaveAttribute('data-tone');
  await expect(badge).toHaveCSS('color', 'rgb(30, 58, 138)');
});

test('chart bar renders the sample with values and the marker between Jul and Aug', async ({
  mount,
  page,
}) => {
  await mount(render(sample(12)));

  const layout = page.getByTestId('layout-chart');
  await expect(layout.getByRole('heading', { level: 2 })).toHaveText(
    'Lead time fell as the slices got smaller',
  );
  await expect(layout.getByTestId('chart-bar')).toHaveCount(6);
  await expect(layout.getByTestId('chart-value')).toHaveText(['11', '10', '8', '5', '3', '2']);
  await expect(layout.getByTestId('chart-x-label')).toHaveText([
    'May',
    'Jun',
    'Jul',
    'Aug',
    'Sep',
    'Oct',
  ]);
  await expect(layout.getByTestId('chart-tick')).toHaveText(['0', '2', '4', '6', '8', '10', '12']);
  await expect(layout.getByTestId('chart-unit')).toHaveText('wk');
  await expect(layout.getByTestId('chart-marker-label')).toHaveText('Flags on');
  await expect(layout.getByTestId('chart-legend-item')).toHaveCount(0);
  await expect(layout.getByTestId('block-callout')).toHaveText(
    'Weeks from first commit to the first customer. The median fell from 11 to 2.',
  );

  // Taller bars for larger values, all on one baseline.
  const bars = await layout
    .getByTestId('chart-bar')
    .evaluateAll((els) => els.map((el) => el.getBoundingClientRect()));
  expect(bars[0].height).toBeGreaterThan(bars[5].height);
  for (const bar of bars) expect(bar.bottom).toBeCloseTo(bars[0].bottom, 0);
  // Marker x 3.5 sits halfway between the third and fourth bars' centres.
  const marker = (await layout.getByTestId('chart-marker').boundingBox())!;
  const between = (bars[2].x + bars[2].width / 2 + bars[3].x + bars[3].width / 2) / 2;
  expect(marker.x + marker.width / 2).toBeCloseTo(between, 0);
});

test('chart line with marker', async ({ mount, page }) => {
  await mount(
    render(
      chartSlide({
        headline: 'Deploys per week rose',
        kind: 'line',
        xLabels: ['W1', 'W2', 'W3', 'W4', 'W5'],
        series: [{ name: 'Deploys', values: [2, 3, 3, 8, 12] }],
        marker: { x: 3, label: 'Flags on' },
      }),
    ),
  );
  const layout = page.getByTestId('layout-chart');
  await expect(layout.getByTestId('chart-line')).toHaveCount(1);
  await expect(layout.getByTestId('chart-line')).toHaveCSS('stroke', ACCENT);
  await expect(layout.getByTestId('chart-line')).toHaveCSS('fill', 'none');
  await expect(layout.getByTestId('chart-point')).toHaveCount(5);
  await expect(layout.getByTestId('chart-bar')).toHaveCount(0);
  await expect(layout.getByTestId('chart-unit')).toHaveCount(0);
  // No caption and one series: no aside, so the plot spans the body.
  await expect(layout.locator('aside')).toHaveCount(0);
  await expect(layout.getByTestId('chart-marker')).toHaveCSS('stroke-dasharray', '6px, 8px');
  await expect(layout.getByTestId('chart-marker-label')).toHaveText('Flags on');

  const points = await layout.getByTestId('chart-point').evaluateAll((els) =>
    els.map((el) => {
      const r = el.getBoundingClientRect();
      return { x: r.x + r.width / 2, y: r.y + r.height / 2 };
    }),
  );
  // Higher values draw higher; equal values sit level.
  expect(points[4].y).toBeLessThan(points[0].y);
  expect(points[1].y).toBeCloseTo(points[2].y, 0);
  // Marker x 3 sits on the third point.
  const marker = (await layout.getByTestId('chart-marker').boundingBox())!;
  expect(marker.x + marker.width / 2).toBeCloseTo(points[2].x, 0);
  const plot = (await layout.getByTestId('chart-plot').boundingBox())!;
  expect(plot.width).toBeCloseTo(1680, 0);
});

test('chart bar with three series', async ({ mount, page }) => {
  await mount(
    render(
      chartSlide({
        kicker: 'Three teams',
        headline: 'Every team got faster',
        kind: 'bar',
        xLabels: ['Q1', 'Q2', 'Q3', 'Q4'],
        series: [
          { name: 'Payments', values: [10, 8, 6, 4] },
          { name: 'Search', values: [12, 9, 7, 3] },
          { name: 'Checkout', values: [9, 9, 5, -2] },
        ],
        yUnit: 'days',
        caption: 'Lead time per quarter.',
      }),
    ),
  );
  const layout = page.getByTestId('layout-chart');
  const bars = layout.getByTestId('chart-bar');
  await expect(bars).toHaveCount(12);
  await expect(layout.locator('[data-testid="chart-bar"][data-series="0"]')).toHaveCount(4);
  await expect(layout.locator('[data-testid="chart-bar"][data-series="0"]').first()).toHaveCSS(
    'fill',
    ACCENT,
  );
  await expect(layout.locator('[data-testid="chart-bar"][data-series="1"]').first()).toHaveCSS(
    'fill',
    WARN,
  );
  await expect(layout.locator('[data-testid="chart-bar"][data-series="2"]').first()).toHaveCSS(
    'fill',
    'rgb(17, 24, 39)',
  );
  // Values over bars are left out when bars are grouped.
  await expect(layout.getByTestId('chart-value')).toHaveCount(0);
  await expect(layout.getByTestId('chart-legend-item')).toHaveText([
    'Payments',
    'Search',
    'Checkout',
  ]);
  await expect(layout.getByTestId('chart-unit')).toHaveText('days');
  // A negative value draws below the zero line, and the ticks reach it.
  await expect(layout.getByTestId('chart-tick').first()).toHaveText('-2');
  const rects = await bars.evaluateAll((els) => els.map((el) => el.getBoundingClientRect()));
  const zero = rects[0].bottom;
  expect(rects[11].top).toBeCloseTo(zero, 0);
  // Grouped bars side by side within each x slot.
  expect(rects[0].right).toBeLessThan(rects[4].left);
  expect(rects[4].right).toBeLessThan(rects[8].left);
  expect(rects[8].right).toBeLessThan(rects[1].left);
});

// The chart's own canvas-px box for a test id, from the slide's top left.
async function boxes(page: Page, testId: string) {
  return page.getByTestId(testId).evaluateAll((els: Element[]) =>
    els.map((el) => {
      const slide = el.closest('[data-layout]')!.getBoundingClientRect();
      const r = el.getBoundingClientRect();
      return { left: r.left - slide.left, right: r.right - slide.left };
    }),
  );
}

// Short tick labels leave the axis narrow; the unit must still fit left of
// the plot without running past the content box's x 120.
for (const kind of ['line', 'bar'] as const) {
  for (const yUnit of ['Feedback', 'WWWWWWWW', 'MMMMMMMM']) {
    test(`chart y unit stays inside the content box: ${kind}, ${yUnit}`, async ({
      mount,
      page,
    }) => {
      await mount(
        render(
          chartSlide({
            kicker: 'Really Is The Team Ships A Small Sliceee',
            headline:
              'Of Work To Customers Every Week And The Feedback Arrives In Days Rather Thaneeee',
            kind,
            xLabels: ['A', 'B'],
            series: [{ name: 'Score', values: [1, 2] }],
            yUnit,
          }),
        ),
      );
      await page.evaluate(() => document.fonts.ready);
      const [unit] = await boxes(page, 'chart-unit');
      const ticks = await boxes(page, 'chart-tick');
      const marks = await boxes(page, kind === 'bar' ? 'chart-bar' : 'chart-point');
      expect(unit.left).toBeGreaterThanOrEqual(120);
      for (const tick of ticks) expect(tick.left).toBeGreaterThanOrEqual(120);
      // The unit still sits left of the plot.
      expect(unit.right).toBeLessThanOrEqual(Math.min(...marks.map((m) => m.left)));
    });
  }
}

test('chart tick labels stay unique in a narrow band at large magnitude', async ({
  mount,
  page,
}) => {
  await mount(
    render(
      chartSlide({
        headline: 'Weekly users held steady',
        kind: 'line',
        xLabels: ['W1', 'W2', 'W3'],
        series: [{ name: 'Users', values: [101_400, 101_900, 102_300] }],
      }),
    ),
  );
  const labels = await page.getByTestId('chart-tick').allTextContents();
  expect(labels).toEqual(['101.4K', '101.6K', '101.8K', '102K', '102.2K', '102.4K']);
  expect(new Set(labels).size).toBe(labels.length);
});

test('chart svg names its kind and series and describes its data', async ({ mount, page }) => {
  await mount(
    render(
      chartSlide({
        headline: 'Every team got faster',
        kind: 'bar',
        xLabels: ['Q1', 'Q2'],
        series: [
          { name: 'Payments', values: [10, 8] },
          { name: 'Search', values: [12, 37500] },
        ],
        yUnit: 'days',
        marker: { x: 1.5, label: 'Flags on' },
      }),
    ),
  );
  const plot = page.getByTestId('chart-plot');
  await expect(plot).toHaveAttribute('role', 'img');
  await expect(plot).toHaveAccessibleName('Bar chart: Payments, Search');
  await expect(plot).toHaveAccessibleDescription(
    'Payments (days): Q1 10, Q2 8. Search (days): Q1 12, Q2 37,500. Marker "Flags on" between Q1 and Q2.',
  );
  await expect(plot.locator('title')).toHaveText('Bar chart: Payments, Search');
});

test('chart marker label turns left at the plot right edge', async ({ mount, page }) => {
  // The last slot's centre plus a 10-character label passes the plot's right
  // edge (24 px in from the svg's) but not the svg's own.
  await mount(
    render(
      chartSlide({
        headline: 'Deploys per week rose',
        kind: 'line',
        xLabels: ['W1', 'W2', 'W3', 'W4', 'W5'],
        series: [{ name: 'Deploys', values: [2, 3, 3, 8, 12] }],
        marker: { x: 5, label: 'Launch day' },
      }),
    ),
  );
  await page.evaluate(() => document.fonts.ready);
  const [marker] = await boxes(page, 'chart-marker');
  const [label] = await boxes(page, 'chart-marker-label');
  await expect(page.getByTestId('chart-marker-label')).toHaveAttribute('text-anchor', 'end');
  expect(label.right).toBeLessThanOrEqual(marker.left);
});

test('gantt statuses', async ({ mount, page }) => {
  await mount(render(sample(5)));

  const layout = page.getByTestId('layout-gantt');
  await expect(layout.getByTestId('gantt-kicker')).toHaveText('What it looked like');
  await expect(layout.getByTestId('gantt-column')).toHaveText([
    'Sprint 1',
    'Sprint 2',
    'Sprint 3',
    'Sprint 4',
    'Sprint 5',
    'Sprint 6',
  ]);
  await expect(layout.getByTestId('gantt-label')).toHaveText([
    'Checkout API',
    'Web checkout',
    'Mobile checkout',
    'Payments partner',
    'Customers see it',
  ]);
  const bars = layout.getByTestId('gantt-bar');
  await expect(bars).toHaveText(['Done', 'Done', 'Slips', 'Stopped', '▲ Ship']);
  await expect(bars.nth(0)).toHaveAttribute('data-status', 'done');
  await expect(bars.nth(0)).toHaveCSS('background-color', ACCENT);
  await expect(bars.nth(0)).toHaveCSS('color', WHITE);
  await expect(bars.nth(2)).toHaveAttribute('data-status', 'slips');
  await expect(bars.nth(2)).toHaveCSS('background-image', /repeating-linear-gradient/);
  await expect(bars.nth(3)).toHaveAttribute('data-status', 'stopped');
  await expect(bars.nth(3)).toHaveCSS('color', BAD);
  await expect(bars.nth(3)).toHaveCSS('box-shadow', `${BAD} 0px 0px 0px 3px inset`);
  await expect(bars.nth(4)).toHaveAttribute('data-status', 'ship');
  await expect(bars.nth(4)).toHaveCSS('color', GOOD);

  // start and end are 1-based column numbers, end inclusive.
  const heads = await layout
    .getByTestId('gantt-column')
    .evaluateAll((els) => els.map((el) => el.getBoundingClientRect()));
  const box = async (i: number) => (await bars.nth(i).boundingBox())!;
  const api = await box(0); // 1 to 2
  expect(api.x).toBeCloseTo(heads[0].left, 0);
  expect(api.x + api.width).toBeCloseTo(heads[1].right, 0);
  const mobile = await box(2); // 3 to 5
  expect(mobile.x).toBeCloseTo(heads[2].left, 0);
  expect(mobile.x + mobile.width).toBeCloseTo(heads[4].right, 0);
  const ship = await box(4); // 6, centred
  expect(ship.x + ship.width / 2).toBeCloseTo((heads[5].left + heads[5].right) / 2, 0);
});

test('gantt bar shows its status when there is no note', async ({ mount, page }) => {
  await mount(
    render({
      ...sample(5),
      layout: 'gantt',
      slots: {
        headline: 'Plain statuses',
        columns: ['A', 'B'],
        rows: [
          { label: 'One', start: 1, end: 1, status: 'stopped' },
          { label: 'Two', start: 2, end: 2, status: 'ship' },
        ],
        caption: 'Under the timeline',
      },
    } as RenderableSlide),
  );
  await expect(page.getByTestId('gantt-bar')).toHaveText(['Stopped', '▲ Ship']);
  await expect(page.getByTestId('gantt-caption')).toHaveText('Under the timeline');
});

// Gantt labels of nothing but W and M, the widest letters, wrap to three
// lines at the 30-character limit. They stop at two with an ellipsis and keep
// their full text in a tooltip, so every row keeps its 72 px. The rest of the
// slide is the gantt's wide-letter max case.
test('gantt labels of W and M only stop at two lines', async ({ mount, page }) => {
  const { slots } = wideTwin(DATA_FIT_FIXTURES.ganttTitle) as Extract<
    DataFitFixture,
    { layout: 'gantt' }
  >;
  const solid = (length: number) =>
    'WMWMW MWMWM '.repeat(3).slice(0, length).trimEnd().padEnd(length, 'M');
  const rows = slots.rows.map((row) => ({ ...row, label: solid(row.label.length) }));
  expect(rows[0].label).toHaveLength(30);
  await mount(
    render({ ...sample(5), layout: 'gantt', slots: { ...slots, rows } } as RenderableSlide),
  );
  const { drawn } = await measure(page, 'layout-gantt');
  console.log(`ganttLabelsWM: drawn bottom ${drawn.bottom.toFixed(1)}`);
  const labels = page.getByTestId('gantt-label');
  await expect(labels.first()).toHaveAttribute('title', rows[0].label);
  const heights = await labels.evaluateAll((els) =>
    els.map((el) => el.getBoundingClientRect().height),
  );
  for (const height of heights) expect(height).toBeLessThanOrEqual(2 * 28 * 1.2 + 1);
  expect(drawn.left).toBeGreaterThanOrEqual(120);
  expect(drawn.right).toBeLessThanOrEqual(1800);
  expect(drawn.bottom).toBeLessThanOrEqual(996);
});

// The shared max-content fixtures (src/contracts/fit-fixtures.json), each in
// its own letters (Title Case or prose) and as a wide-letter twin of the same
// lengths. Everything drawn stays inside x 120–1800 and y 150–996; a table
// also stays inside LayoutRules.Table's estimate (to the pixel, as its CSS
// follows that geometry), except for its wide twin, which the server's
// average-width estimate does not cover (SW-130).
const MAX_CASES = Object.entries(DATA_FIT_FIXTURES).flatMap(([name, fixture]) => [
  { name, fixture, wide: false },
  ...(name.endsWith('Title')
    ? [{ name: `${name.replace(/Title$/, '')}Wide`, fixture: wideTwin(fixture), wide: true }]
    : []),
]);

for (const { name, fixture, wide } of MAX_CASES) {
  test(`max counts stay inside 1920x1080: ${name}`, async ({ mount, page }) => {
    const { layout, slots, estimate } = fixture;
    // red until SW-130: wide-letter cells at the table's accepted lengths wrap
    // well past SlideFit's 0.56 em count (they end near y = 1199), and the
    // table's CSS can't absorb that at the server's geometry.
    test.fail(wide && layout === 'table', 'red until SW-130');
    await stubApi(page, {});
    await mount(render({ ...sample(3), layout, slots } as RenderableSlide));
    if (layout === 'image') await loaded(page, 'image-picture');

    const { drawn, boxTop, body, headline, footerTop } = await measure(page, `layout-${layout}`);
    console.log(
      `${name}: body top ${body.top.toFixed(1)}, drawn top ${drawn.top.toFixed(1)}, drawn bottom ${drawn.bottom.toFixed(1)}` +
        (estimate !== undefined ? `, estimate bottom ${(body.top + estimate).toFixed(1)}` : ''),
    );
    expect(headline.bottom - headline.top).toBeLessThanOrEqual(2 * 64 * 1.08 + 1);
    expect(footerTop).toBeGreaterThanOrEqual(1023);
    expect(boxTop).toBeGreaterThanOrEqual(150);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    if (estimate !== undefined && !wide) {
      expect(drawn.bottom).toBeLessThanOrEqual(body.top + estimate + 1);
    }
    expect(drawn.bottom).toBeLessThanOrEqual(996);
  });
}

test('screenshot: image', async ({ mount, page }) => {
  await stubApi(page, {});
  const component = await mount(render(imageSlide(IMAGE_SLOTS)));
  await loaded(page, 'image-picture');
  await expect(component).toHaveScreenshot('layout-image.png');
});

test('screenshot: table', async ({ mount }) => {
  const component = await mount(render(sample(11)));
  await expect(component).toHaveScreenshot('layout-table.png');
});

test('screenshot: chart', async ({ mount }) => {
  const component = await mount(render(sample(12)));
  await expect(component).toHaveScreenshot('layout-chart.png');
});

test('screenshot: gantt', async ({ mount }) => {
  const component = await mount(render(sample(5)));
  await expect(component).toHaveScreenshot('layout-gantt.png');
});
