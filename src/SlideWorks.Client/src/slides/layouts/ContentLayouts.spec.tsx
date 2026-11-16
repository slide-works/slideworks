import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { SlideDeck } from '../slots';
import { MAX_LAYOUT, MAX_SLOTS } from './ContentLayouts.fixtures';

// The bullets, two-column, practice and compare layouts with the callout
// block, rendered through SlideRenderer inside the real frame.

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
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const GOOD = 'rgb(22, 163, 74)';
const BAD = 'rgb(220, 38, 38)';

type Box = { left: number; top: number; right: number; bottom: number };

// What the layout draws, in canvas px from the slide's top left: the body's own
// box, and the extent of every element and run of text inside the layout (text
// that overflows its element is caught by its own rects). Also the footer's top.
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
    for (const element of root.querySelectorAll('*')) add(element.getBoundingClientRect());
    // A 20 px/1 kicker's glyphs rise a little above its line box; the boxes don't.
    const boxTop = drawn.top;
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
    /rgb\(22, 163, 74\) 6px 0px 0px 0px inset/,
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
  await expect(failure).toHaveAttribute('data-tone', 'bad');
  await expect(failure).toHaveCSS('border-top-color', BAD);
  await expect(failure.getByTestId('compare-label')).toHaveText('Failure mode');
  await expect(failure.getByTestId('compare-label')).toHaveCSS('color', BAD);
  await expect(failure.getByRole('heading', { level: 3 })).toHaveText('Slice by layer');
  await expect(failure.getByTestId('compare-sub')).toHaveCount(0);
  await expect(failure.getByTestId('compare-point')).toHaveText([
    'Database, then API, then UI',
    'Nothing to show for five sprints',
    'The first customer feedback rewrites the API',
  ]);

  const success = layout.getByTestId('compare-success');
  await expect(success).toHaveAttribute('data-tone', 'good');
  await expect(success).toHaveCSS('border-top-color', GOOD);
  await expect(success.getByTestId('compare-label')).toHaveText('Success mode');
  await expect(success.getByTestId('compare-label')).toHaveCSS('color', GOOD);
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

for (const [name, slots] of Object.entries(MAX_SLOTS)) {
  const layout = MAX_LAYOUT[name as keyof typeof MAX_SLOTS];
  test(`max-length text stays inside 1920x1080: ${name}`, async ({ mount, page }) => {
    await stubApi(page, {});
    const steps = 'items' in slots ? slots.items.length : 0;
    await mount(render({ ...sample(3), layout, slots } as RenderableSlide, steps));

    const image = page.getByTestId('two-column-image');
    if ((await image.count()) > 0) {
      await expect
        .poll(() => image.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth))
        .toBeGreaterThan(0);
    }

    const { drawn, boxTop, body, headline, footerTop } = await measure(page, `layout-${layout}`);
    console.log(`${name}: body ${body.top}–${body.bottom}, drawn bottom ${drawn.bottom}`);
    // The server's estimate assumes a headline of at most two 69 px lines.
    expect(headline.bottom - headline.top).toBeLessThanOrEqual(2 * 64 * 1.08 + 1);
    expect(footerTop).toBeGreaterThanOrEqual(1023);
    expect(body.bottom).toBeLessThanOrEqual(996);
    // Everything drawn stays in the content box, and runs no lower than y = 996.
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    expect(boxTop).toBeGreaterThanOrEqual(150);
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
