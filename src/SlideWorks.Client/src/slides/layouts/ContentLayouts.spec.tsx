import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { paintedExtent, slideBox } from '../../test-support/measure/paintedExtent';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { SlideDeck } from '../slots';
import { FIT_FIXTURES } from './ContentLayouts.fixtures';

// The bullets, two-column, practice and compare layouts with the callout
// block, rendered through SlideRenderer inside the real frame.

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
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const GOOD = 'rgb(21, 128, 61)';
const BAD = 'rgb(220, 38, 38)';

// A palette token's computed colour inside an element, so an assertion names
// the token rather than a colour.
function tokenColour(page: Page, testId: string, key: string): Promise<string> {
  return page.getByTestId(testId).evaluate((element, token) => {
    const probe = document.createElement('span');
    probe.style.color = `var(--sw-${token})`;
    element.appendChild(probe);
    const colour = getComputedStyle(probe).color;
    probe.remove();
    return colour;
  }, key);
}

// paintedExtent plus the headline's box, which the max-text checks bound.
async function measure(page: Page, layoutTestId: string) {
  const extent = await paintedExtent(page, layoutTestId, { images: true });
  const headline = await slideBox(page, layoutTestId, 'h2');
  return { ...extent, headline };
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

test('bullets renders the sample slide with bold leads', async ({ mount, page }) => {
  await mount(render(sample(3), 4));

  const layout = page.getByTestId('layout-bullets');
  await expect(layout.getByTestId('bullets-kicker')).toHaveText('The pattern');
  await expect(layout.getByRole('heading', { level: 2 })).toHaveText(
    'Every slip we had last year started as a big batch',
  );
  const items = layout.getByTestId('bullets-item');
  await expect(items).toHaveCount(4);
  await expect(items.nth(0)).toHaveText(
    'Risk shows up late. The first real customer sees it in week ten.',
  );
  await expect(items.nth(0).locator('b')).toHaveText('Risk shows up late.');
  await expect(items).toHaveText([/^Risk/, /^Merges/, /^Feedback/, /^Nobody/]);
  await expect(layout.getByTestId('block-callout')).toHaveCount(0);
});

test('bullets one-by-one hides later items until their step', async ({ mount, page }) => {
  const component = await mount(render(sample(3), 0));
  const items = page.getByTestId('bullets-item');
  await expect(items).toHaveCount(4);
  for (let i = 0; i < 4; i++) await expect(items.nth(i)).toBeHidden();
  await component.unmount();

  const second = await mount(render(sample(3), 2));
  await expect(items.nth(0)).toBeVisible();
  await expect(items.nth(1)).toBeVisible();
  await expect(items.nth(2)).toBeHidden();
  await expect(items.nth(3)).toBeHidden();
  // Hidden items keep their space: visibility, never display:none.
  const box = await items.nth(3).boundingBox();
  expect(box?.height).toBeGreaterThan(0);
  await expect(page.locator('li[data-revealed]').nth(3)).toHaveCSS('visibility', 'hidden');
  await second.unmount();

  await mount(render(sample(3), 4));
  for (let i = 0; i < 4; i++) await expect(items.nth(i)).toBeVisible();
});

test('bullets with reveal all shows every item at step 0, and the callout', async ({
  mount,
  page,
}) => {
  const slide = sample(3);
  await mount(
    render({
      ...slide,
      layout: 'bullets',
      slots: {
        headline: 'Plain points',
        items: ['First point', 'Second point'],
        callout: { tone: 'good', text: 'A good callout' },
      },
    }),
  );
  const items = page.getByTestId('bullets-item');
  await expect(items).toHaveText(['First point', 'Second point']);
  for (let i = 0; i < 2; i++) await expect(items.nth(i)).toBeVisible();
  await expect(page.getByTestId('bullets-kicker')).toHaveCount(0);
  await expect(page.getByTestId('block-callout')).toHaveText('A good callout');
  await expect(page.getByTestId('block-callout')).toHaveAttribute('data-tone', 'good');
});

test('two-column renders points on the left and the uploaded image on the right', async ({
  mount,
  page,
}) => {
  await stubApi(page, {});
  await mount(render(sample(10)));

  const layout = page.getByTestId('layout-two-column');
  await expect(layout.getByTestId('two-column-kicker')).toHaveText('The tool');
  await expect(layout.getByRole('heading', { level: 2 })).toHaveText(
    'A story map shows the thinnest release',
  );
  await expect(layout.getByTestId('two-column-left').getByTestId('two-column-item')).toHaveText([
    'Across: the steps a customer takes',
    'Down: richer versions of each step',
    'Top row: the first release. It covers every step, done simply.',
  ]);
  const image = layout.getByTestId('two-column-right').getByTestId('two-column-image');
  await expect(image).toHaveAttribute('src', '/api/images/img_sample0001');
  await expect(image).toHaveAttribute('alt', /^A story map: four customer steps/);
  await expect
    .poll(() => image.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth))
    .toBeGreaterThan(0);
});

test('two-column renders titles and paragraphs', async ({ mount, page }) => {
  await mount(
    render({
      ...sample(10),
      layout: 'two-column',
      slots: {
        headline: 'Two paragraphs',
        left: { title: 'Before', text: 'One launch a quarter.' },
        right: { title: 'After', items: [{ lead: 'Weekly:', text: 'a slice a week.' }] },
      },
    }),
  );
  const left = page.getByTestId('two-column-left');
  await expect(left.getByRole('heading', { level: 3 })).toHaveText('Before');
  await expect(left.getByTestId('two-column-text')).toHaveText('One launch a quarter.');
  const right = page.getByTestId('two-column-right');
  await expect(right.getByRole('heading', { level: 3 })).toHaveText('After');
  await expect(right.getByTestId('two-column-item')).toHaveText('Weekly: a slice a week.');
  await expect(page.getByTestId('two-column-image')).toHaveCount(0);
});

test('two-column image sits on a white card with a shadow, at most twice its size', async ({
  mount,
  page,
}) => {
  await stubImage(page, 'img_small', 120, 80);
  await mount(
    render({
      ...sample(10),
      layout: 'two-column',
      slots: {
        headline: 'A small picture',
        left: { text: 'Next to it.' },
        right: { image: { image: 'img_small', alt: 'A small picture' } },
      },
    }),
  );
  const image = page.getByTestId('two-column-image');
  await expect
    .poll(() => image.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth))
    .toBe(120);
  await expect(image).toHaveCSS('background-color', 'rgb(255, 255, 255)');
  await expect(image).toHaveCSS('box-shadow', /14px 36px/);
  // 120x80 is drawn at 240x160, centred in its column, not stretched to fill it.
  await expect.poll(async () => (await image.boundingBox())?.width).toBe(240);
  const box = (await image.boundingBox())!;
  const column = (await page.getByTestId('two-column-right').boundingBox())!;
  expect(box.height).toBeCloseTo(160, 0);
  expect(box.x - column.x).toBeCloseTo((column.width - 240) / 2, 0);
});

test('two-column image fits a tall picture to the height that is left', async ({ mount, page }) => {
  await stubImage(page, 'img_tall', 400, 2000);
  await mount(
    render({
      ...sample(10),
      layout: 'two-column',
      slots: {
        headline: 'A tall picture',
        left: { text: 'Next to it.' },
        right: { title: 'Tall', image: { image: 'img_tall', alt: 'A tall picture' } },
      },
    }),
  );
  const image = page.getByTestId('two-column-image');
  await expect
    .poll(() => image.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth))
    .toBe(400);
  const { body } = await measure(page, 'layout-two-column');
  await expect
    .poll(async () => {
      const box = (await image.boundingBox())!;
      return Math.round(box.width * 5 - box.height);
    })
    .toBe(0);
  const box = (await image.boundingBox())!;
  const slide = (await page.getByTestId('slide-frame').boundingBox())!;
  expect(box.y + box.height - slide.y).toBeLessThanOrEqual(body.bottom + 0.5);
});

test('practice shows do comes-back avoid and callout tone', async ({ mount, page }) => {
  await mount(render(sample(8)));

  const layout = page.getByTestId('layout-practice');
  await expect(layout.getByTestId('practice-kicker')).toHaveText('Slice it');
  await expect(layout.getByRole('heading', { level: 2 })).toHaveText(
    'Slice by what a customer can do, not by layer',
  );

  const doList = layout.getByTestId('practice-do');
  await expect(doList.getByRole('heading', { level: 3 })).toHaveText('Do');
  await expect(doList.getByTestId('practice-card')).toHaveCount(4);
  await expect(doList.getByTestId('practice-card').nth(0)).toHaveText(
    'Start from one user action and build every layer it needs, thinly.',
  );
  await expect(doList.getByTestId('practice-card').nth(0).locator('b')).toHaveText(
    'Start from one user action',
  );

  const comesBack = layout.getByTestId('practice-comes-back');
  await expect(comesBack.getByRole('heading', { level: 3 })).toHaveText('What comes back');
  await expect(comesBack.getByRole('heading', { level: 3 })).toHaveCSS('color', GOOD);
  await expect(comesBack.getByTestId('practice-card')).toHaveText([
    'Feedback in days, not quarters.',
    "A merge that's small enough to review in one sitting.",
  ]);
  await expect(comesBack.getByTestId('practice-card').nth(0)).toHaveCSS(
    'box-shadow',
    /rgb\(21, 128, 61\) 6px 0px 0px 0px inset/,
  );

  const avoid = layout.getByTestId('practice-avoid');
  await expect(avoid.getByRole('heading', { level: 3 })).toHaveText('Avoid');
  await expect(avoid.getByRole('heading', { level: 3 })).toHaveCSS('color', BAD);
  await expect(avoid.getByTestId('practice-card')).toHaveText([
    '"API first, UI later." That\'s two batches, not two slices.',
  ]);
  await expect(avoid.getByTestId('practice-card').nth(0)).toHaveCSS(
    'box-shadow',
    /rgb\(220, 38, 38\) 6px 0px 0px 0px inset/,
  );

  const callout = layout.getByTestId('block-callout');
  await expect(callout).toHaveText(
    "If a slice can't be demoed to a customer, it isn't a slice yet.",
  );
  await expect(callout).toHaveAttribute('data-tone', 'bad');
});

test('practice leaves out an empty list with its header', async ({ mount, page }) => {
  await mount(
    render({
      ...sample(8),
      layout: 'practice',
      slots: {
        headline: 'Only do',
        do: [{ lead: 'One thing', text: 'at a time.' }],
        comes_back: [],
        avoid: ['Two things'],
      },
    }),
  );
  await expect(page.getByTestId('practice-comes-back')).toHaveCount(0);
  await expect(page.getByTestId('practice-avoid').getByTestId('practice-card')).toHaveText([
    'Two things',
  ]);
  await expect(page.getByTestId('block-callout')).toHaveCount(0);
});

test('compare failure and success panels', async ({ mount, page }) => {
  await mount(render(sample(9)));

  const layout = page.getByTestId('layout-compare');
  await expect(layout.getByTestId('compare-kicker')).toHaveText('Same feature, two ways');
  await expect(layout.getByRole('heading', { level: 2 })).toHaveText(
    'The difference is when the customer gets to answer',
  );

  const failure = layout.getByTestId('compare-failure');
  await expect(failure).toHaveAttribute('data-tone', 'negative');
  await expect(failure).toHaveCSS('border-top-color', BAD);
  await expect(failure.getByTestId('compare-label')).toHaveText('Failure mode');
  // The tag is text, so it takes the panel's ink, not the role colour (SW-72 D2).
  await expect(failure.getByTestId('compare-label')).toHaveCSS(
    'color',
    await tokenColour(page, 'compare-failure', 'bad-ink'),
  );
  await expect(failure.getByRole('heading', { level: 3 })).toHaveText('Slice by layer');
  await expect(failure.getByTestId('compare-sub')).toHaveCount(0);
  await expect(failure.getByTestId('compare-point').nth(0)).toHaveCSS('list-style-type', 'disc');
  await expect(failure.getByTestId('compare-point')).toHaveText([
    'Database, then API, then UI',
    'Nothing to show for five sprints',
    'The first customer feedback rewrites the API',
  ]);

  const success = layout.getByTestId('compare-success');
  await expect(success).toHaveAttribute('data-tone', 'positive');
  await expect(success).toHaveCSS('border-top-color', GOOD);
  await expect(success.getByTestId('compare-label')).toHaveText('Success mode');
  await expect(success.getByTestId('compare-label')).toHaveCSS(
    'color',
    await tokenColour(page, 'compare-success', 'good-ink'),
  );
  await expect(success.getByRole('heading', { level: 3 })).toHaveText('Slice by customer action');
  await expect(success.getByTestId('compare-point')).toHaveText([
    '"Save a card", then "pay with a saved card"',
    'Something to demo every week',
    'Feedback changes week 2, not week 10',
  ]);

  const left = await failure.boundingBox();
  const right = await success.boundingBox();
  expect(left!.x).toBeLessThan(right!.x);
});

test('compare shows a panel sub when set', async ({ mount, page }) => {
  await mount(
    render({
      ...sample(9),
      layout: 'compare',
      slots: {
        headline: 'Two teams',
        failure: { label: 'Checkout', title: 'By layer', sub: 'One launch', points: ['Late'] },
        success: { label: 'Search', title: 'By action', sub: 'Nine releases', points: ['Weekly'] },
      },
    }),
  );
  await expect(page.getByTestId('compare-sub')).toHaveText(['One launch', 'Nine releases']);
});

// The shared max-content fixtures (src/contracts/fit-fixtures.json): Title
// Case text at the most the server accepts. Everything drawn stays within
// LayoutRules' estimate below the body's top (to the pixel, as the CSS follows
// its geometry) and above y = 996, so a layout that draws taller than the
// server counts fails here.
for (const [name, fixture] of Object.entries(FIT_FIXTURES)) {
  test(`max-length text stays inside 1920x1080: ${name}`, async ({ mount, page }) => {
    await stubApi(page, {});
    const { layout, slots, estimate } = fixture;
    const steps = 'items' in slots ? slots.items.length : 0;
    await mount(render({ ...sample(3), layout, slots } as RenderableSlide, steps));

    const image = page.getByTestId('two-column-image');
    if ((await image.count()) > 0) {
      await expect
        .poll(() => image.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth))
        .toBeGreaterThan(0);
    }

    const { drawn, boxTop, body, headline, footerTop } = await measure(page, `layout-${layout}`);
    console.log(
      `${name}: body top ${body.top}, drawn bottom ${drawn.bottom}, estimate bottom ${body.top + estimate}`,
    );
    // The server's estimate assumes a headline of at most two 69 px lines, so
    // the body starts at y = 150 + 42 + 2 × 69.12 + 46.
    expect(headline.bottom - headline.top).toBeLessThanOrEqual(2 * 64 * 1.08 + 1);
    expect(body.top).toBeCloseTo(376.2, 0);
    expect(footerTop).toBeGreaterThanOrEqual(1023);
    expect(body.bottom).toBeLessThanOrEqual(996);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    expect(boxTop).toBeGreaterThanOrEqual(150);
    expect(drawn.bottom).toBeLessThanOrEqual(body.top + estimate + 1);
    expect(drawn.bottom).toBeLessThanOrEqual(996);
  });
}

test('screenshot: bullets', async ({ mount }) => {
  const component = await mount(render(sample(3), 4));
  await expect(component).toHaveScreenshot('layout-bullets.png');
});

test('screenshot: two-column', async ({ mount, page }) => {
  await stubApi(page, {});
  const component = await mount(render(sample(10)));
  const image = page.getByTestId('two-column-image');
  await expect
    .poll(() => image.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth))
    .toBeGreaterThan(0);
  await expect(component).toHaveScreenshot('layout-two-column.png');
});

test('screenshot: practice', async ({ mount }) => {
  const component = await mount(render(sample(8)));
  await expect(component).toHaveScreenshot('layout-practice.png');
});

test('screenshot: compare', async ({ mount }) => {
  const component = await mount(render(sample(9)));
  await expect(component).toHaveScreenshot('layout-compare.png');
});
