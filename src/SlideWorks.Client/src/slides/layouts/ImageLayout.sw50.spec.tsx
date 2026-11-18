import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { ImageSlots } from '../slots/image';
import type { SlideDeck } from '../slots';

// The image layout's SW-50 additions: a frame that can be left off, the image
// on the right at full height, a callout, points that arrive one per step and
// carry a lead and an icon, and emphasis in the headline.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function imageSlide(slots: ImageSlots, number = 10): RenderableSlide {
  return { number, layout: 'image', part: null, deepDive: false, surface: undefined, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

// An SVG image of the given natural size, served for every /api/images/<id>.
async function stubImages(page: Page, width: number, height: number) {
  await page.route('**/api/images/*', (route) =>
    route.fulfill({
      contentType: 'image/svg+xml',
      body: `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}"><rect width="${width}" height="${height}" fill="#2563eb"/></svg>`,
    }),
  );
}

async function loaded(page: Page) {
  const image = page.getByTestId('image-picture');
  await expect
    .poll(() => image.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth))
    .toBeGreaterThan(0);
  return image;
}

const base: ImageSlots = {
  headline: 'A story map',
  image: { image: 'img_pic', alt: 'A story map: four steps across, releases down' },
};

const box = (page: Page, testId: string) => page.getByTestId(testId).boundingBox();

test('frame none draws no card', async ({ mount, page }) => {
  await stubImages(page, 800, 500);
  await mount(render(imageSlide({ ...base, frame: 'none' })));
  const image = await loaded(page);
  await expect(page.getByTestId('layout-image')).toHaveAttribute('data-frame', 'none');
  await expect(image).toHaveCSS('background-color', 'rgba(0, 0, 0, 0)');
  await expect(image).toHaveCSS('box-shadow', 'none');
  await expect(image).toHaveCSS('border-top-left-radius', '0px');
});

test('the default frame keeps the card', async ({ mount, page }) => {
  await stubImages(page, 800, 500);
  await mount(render(imageSlide(base)));
  const image = await loaded(page);
  await expect(page.getByTestId('layout-image')).toHaveAttribute('data-frame', 'card');
  await expect(image).toHaveCSS('background-color', 'rgb(255, 255, 255)');
  await expect(image).not.toHaveCSS('box-shadow', 'none');
});

test('position right puts the image beside the headline', async ({ mount, page }) => {
  await stubImages(page, 792, 882);
  await mount(
    render(
      imageSlide({
        ...base,
        position: 'right',
        points: ['One row is one release.', 'One column is one step.'],
      }),
    ),
  );
  const image = await loaded(page);
  await expect(page.getByTestId('layout-image')).toHaveAttribute('data-position', 'right');
  await expect.poll(async () => (await image.boundingBox())?.width).toBeGreaterThanOrEqual(700);
  const picture = (await image.boundingBox())!;
  const headline = (await box(page, 'image-headline'))!;
  const point = (await page.getByTestId('image-point').first().boundingBox())!;
  expect(picture.x + picture.width).toBeCloseTo(1800, 0);
  expect(headline.x + headline.width).toBeLessThanOrEqual(880);
  expect(picture.x).toBeGreaterThanOrEqual(headline.x + 760);
  expect(point.x + point.width).toBeLessThanOrEqual(880);
  expect(point.y).toBeGreaterThan(headline.y + headline.height);
});

test('position right keeps the callout at the foot of the left column', async ({ mount, page }) => {
  await stubImages(page, 792, 882);
  await mount(
    render(
      imageSlide({
        ...base,
        position: 'right',
        points: ['One row is one release.'],
        callout: { tone: 'info', text: 'Read it left to right.' },
      }),
    ),
  );
  await loaded(page);
  const callout = (await box(page, 'body-callout'))!;
  const point = (await page.getByTestId('image-point').boundingBox())!;
  expect(callout.x).toBeCloseTo(120, 0);
  expect(callout.width).toBeCloseTo(760, 0);
  expect(callout.y).toBeGreaterThan(point.y + point.height);
  expect(callout.y + callout.height).toBeLessThanOrEqual(996);
});

test('point n appears on step n and keeps its space', async ({ mount, page }) => {
  await stubImages(page, 800, 500);
  const slots: ImageSlots = { ...base, points: ['One', 'Two', 'Three'], reveal: 'one-by-one' };
  const component = await mount(render(imageSlide(slots), 0));
  const items = page.getByTestId('layout-image').locator('li.sw-reveal');
  await expect(items).toHaveCount(3);
  const before = await Promise.all([0, 1, 2].map((i) => items.nth(i).boundingBox()));
  for (let step = 0; step <= 3; step++) {
    await component.update(render(imageSlide(slots), step));
    for (let i = 0; i < 3; i++) {
      await expect(items.nth(i)).toHaveAttribute('data-revealed', String(step >= i + 1));
      expect(await items.nth(i).boundingBox()).toEqual(before[i]);
    }
  }
});

test('a callout that reveals last follows the points', async ({ mount, page }) => {
  await stubImages(page, 800, 500);
  const slots: ImageSlots = {
    ...base,
    points: ['One', 'Two'],
    reveal: 'one-by-one',
    callout: { tone: 'info', text: 'Last', reveal: 'last' },
  };
  const component = await mount(render(imageSlide(slots), 2));
  const callout = page.getByTestId('body-callout').locator('.sw-reveal');
  await expect(callout).toHaveAttribute('data-revealed', 'false');
  await component.update(render(imageSlide(slots), 3));
  await expect(callout).toHaveAttribute('data-revealed', 'true');
});

test('a callout with reveal all shows from the start', async ({ mount, page }) => {
  await stubImages(page, 800, 500);
  await mount(
    render(
      imageSlide({
        ...base,
        points: ['One'],
        reveal: 'one-by-one',
        callout: { tone: 'info', text: 'Now' },
      }),
    ),
  );
  await expect(page.getByTestId('body-callout').locator('.sw-reveal')).toHaveCount(0);
  await expect(page.getByTestId('body-callout')).toContainText('Now');
});

test('a two-line callout leaves the image at least 300 px tall', async ({ mount, page }) => {
  await stubImages(page, 600, 900);
  const slots = fitFixture('imageCallout').slots as ImageSlots;
  await mount(render(imageSlide({ ...slots, callout: { ...slots.callout!, reveal: undefined } })));
  const image = await loaded(page);
  await expect.poll(async () => (await image.boundingBox())?.height).toBeGreaterThanOrEqual(300);
  const callout = (await box(page, 'body-callout'))!;
  const picture = (await image.boundingBox())!;
  expect(callout.height).toBeGreaterThan(120);
  expect(callout.y).toBeGreaterThanOrEqual(picture.y + picture.height);
  expect(callout.y + callout.height).toBeLessThanOrEqual(996);
});

test('points carry a lead and an icon in the mark place', async ({ mount, page }) => {
  await stubImages(page, 800, 500);
  await mount(
    render(
      imageSlide({
        ...base,
        points: [{ icon: 'bolt', lead: 'Fast.', text: 'Ship weekly' }, 'Plain'],
      }),
    ),
  );
  const items = page.getByTestId('image-point');
  await expect(page.getByTestId('image-point-icon')).toHaveCount(1);
  await expect(items.first().locator('b')).toHaveText('Fast.');
  await expect(items.first().getByTestId('block-icon')).toHaveAttribute('data-icon', 'bolt');
  expect(await items.first().evaluate((el) => getComputedStyle(el, '::before').content)).toBe(
    'none',
  );
  expect(await items.nth(1).evaluate((el) => getComputedStyle(el, '::before').content)).not.toBe(
    'none',
  );
  const icon = (await box(page, 'block-icon'))!;
  expect(icon.width).toBeCloseTo(34, 0);
});

test('an unknown icon name keeps the bullet', async ({ mount, page }) => {
  await stubImages(page, 800, 500);
  await mount(
    render(
      imageSlide({
        ...base,
        points: [{ icon: 'no-such-icon', text: 'Mystery' }, 'Plain'],
        reveal: 'one-by-one',
      }),
    ),
  );
  await expect(page.getByTestId('image-point-icon')).toHaveCount(0);
  await expect(page.getByTestId('block-icon')).toHaveCount(0);
  const items = page.getByTestId('layout-image').locator('li');
  for (const li of [items.first(), items.nth(1)]) {
    expect(await li.evaluate((el) => getComputedStyle(el, '::before').content)).not.toBe('none');
  }
});

test('headline emphasis carries its tone', async ({ mount, page }) => {
  await stubImages(page, 800, 500);
  await mount(
    render(
      imageSlide({
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

test('an image that fails to load keeps its alt text', async ({ mount, page }) => {
  await page.route('**/api/images/*', (route) => route.abort());
  await mount(render(imageSlide(base)));
  const image = page.getByTestId('image-picture');
  await expect(image).toHaveAttribute('alt', base.image.alt);
  await expect
    .poll(() => image.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth))
    .toBe(0);
  await expect(page.getByRole('img', { name: base.image.alt })).toHaveCount(1);
  await expect(page.getByTestId('image-headline')).toHaveText(base.headline);
});

// The left and right extents, and the bottom, of every text run in the layout.
function textBox(page: Page) {
  return page.getByTestId('layout-image').evaluate((root: Element) => {
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

for (const name of [
  'imageRightMax',
  'imageRightMaxWide',
  'imageRightNoFrame',
  'imageBelowLeadPoints',
  'imageCallout',
]) {
  test(`${name} stays inside the box`, async ({ mount, page }) => {
    await stubApi(page, {});
    await stubImages(page, 792, 882);
    const slots = fitFixture(name).slots as ImageSlots;
    // Every step shown, so a callout that reveals last is drawn.
    await mount(render(imageSlide(slots), 10));
    await loaded(page);
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left).toBeGreaterThanOrEqual(120);
    expect(drawn.right).toBeLessThanOrEqual(1800);
    expect(drawn.bottom).toBeLessThanOrEqual(996);
    test.info().annotations.push({ type: 'bottom', description: `${drawn.bottom}` });
  });
}

test('screenshot: image right', async ({ mount, page }) => {
  await stubImages(page, 792, 882);
  const component = await mount(
    render(
      imageSlide({
        kicker: 'Slice it',
        headline: 'A story map shows the thinnest release',
        image: { image: 'img_pic', alt: 'A story map' },
        position: 'right',
        points: [
          { icon: 'check', lead: 'One row.', text: 'One release.' },
          'One column is one step.',
        ],
        caption: 'The first row shipped in a week.',
        emphasis: [{ text: 'thinnest release', tone: 'accent' }],
      }),
    ),
  );
  await loaded(page);
  await expect(component).toHaveScreenshot('layout-image-right.png');
});

test('screenshot: image no frame', async ({ mount, page }) => {
  await stubImages(page, 800, 500);
  const component = await mount(
    render(imageSlide({ ...base, kicker: 'Slice it', frame: 'none', caption: 'No card.' })),
  );
  await loaded(page);
  await expect(component).toHaveScreenshot('layout-image-no-frame.png');
});

test('screenshot: image with callout', async ({ mount, page }) => {
  await stubImages(page, 800, 500);
  const component = await mount(
    render(
      imageSlide({
        ...base,
        kicker: 'Slice it',
        points: ['One row is one release.', 'One column is one step.'],
        callout: { tone: 'info', text: 'Cut from the right, keep the walking skeleton.' },
      }),
    ),
  );
  await loaded(page);
  await expect(component).toHaveScreenshot('layout-image-callout.png');
});
