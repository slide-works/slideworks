import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import { fitFixture } from '../../test-support/fixtures/fitFixtures';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { stubApi } from '../../test-support/stubApi';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { BigNumberSlots } from '../slots/big-number';
import type { SlideDeck } from '../slots';

// The big-number layout's SW-50 additions: a callout under the number,
// emphasis in the headline and caption, a detail that arrives on a step, a
// value of 10 characters and a dark surface.

test.use({ viewport: { width: 1920, height: 1080 } });

const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

function bigNumber(slots: BigNumberSlots, surface?: 'light' | 'dark', number = 3): RenderableSlide {
  return { number, layout: 'big-number', part: null, deepDive: false, surface, slots };
}

// A plain function, not a component: CT can only mount components from source files.
function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

const base: BigNumberSlots = {
  headline: 'Lead time',
  value: '11 wks',
  caption: 'Median from first commit to a customer',
  detail: 'Eight of our twelve epics.',
};

const box = (page: Page, testId: string) => page.getByTestId(testId).boundingBox();

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

test('dark big-number', async ({ mount, page }) => {
  await mount(render(bigNumber(base, 'dark')));
  const colour = await page
    .getByTestId('big-number-caption')
    .evaluate((el) => getComputedStyle(el).color);
  expect(colour).toBe(await tokenColour(page, 'big-number-caption', 'night-ink'));
});

test('detail hidden keeps its space', async ({ mount, page }) => {
  const slots: BigNumberSlots = { ...base, reveal: 'one-by-one' };
  const component = await mount(render(bigNumber(slots), 0));
  const wrapper = page.getByTestId('big-number-detail').locator('xpath=..');
  await expect(wrapper).toHaveAttribute('data-revealed', 'false');
  const hidden = (await box(page, 'big-number-detail'))!;
  await component.update(render(bigNumber(slots), 1));
  await expect(wrapper).toHaveAttribute('data-revealed', 'true');
  expect((await box(page, 'big-number-detail'))!).toEqual(hidden);
});

test('reveal all shows the detail with no step wrapper', async ({ mount, page }) => {
  await mount(render(bigNumber(base), 0));
  await expect(page.getByTestId('big-number-detail').locator('xpath=..')).not.toHaveClass(
    /sw-reveal/,
  );
});

test('caption emphasis carries its tone', async ({ mount, page }) => {
  await mount(
    render(
      bigNumber({
        ...base,
        caption: 'Median from first commit to a customer',
        emphasis: [{ text: 'a customer', tone: 'positive', in: 'caption' }],
      }),
    ),
  );
  const phrase = page.getByTestId('big-number-caption').getByTestId('emphasis');
  await expect(phrase).toHaveText('a customer');
  await expect(phrase).toHaveAttribute('data-tone', 'positive');
});

test('callout arrives on the step after the detail', async ({ mount, page }) => {
  const slots: BigNumberSlots = {
    ...base,
    reveal: 'one-by-one',
    callout: { tone: 'info', text: 'Last', reveal: 'last' },
  };
  const component = await mount(render(bigNumber(slots), 1));
  const callout = page.getByTestId('body-callout').locator('.sw-reveal');
  await expect(callout).toHaveAttribute('data-revealed', 'false');
  await component.update(render(bigNumber(slots), 2));
  await expect(callout).toHaveAttribute('data-revealed', 'true');
});

test('a two-line callout leaves the value at least 300 px tall', async ({ mount, page }) => {
  const slots = fitFixture('bigNumberCallout').slots as BigNumberSlots;
  await mount(
    render(
      bigNumber({
        ...slots,
        reveal: undefined,
        callout: { ...slots.callout!, reveal: undefined },
      }),
    ),
  );
  await page.evaluate(() => document.fonts.ready);
  const callout = (await box(page, 'body-callout'))!;
  expect(callout.height).toBeGreaterThan(120);
  // The grid above the callout keeps at least 300 px, and the value keeps its full 300 px type.
  const grid = (await page.getByTestId('big-number-column').locator('xpath=..').boundingBox())!;
  expect(grid.height).toBeGreaterThanOrEqual(300);
  expect(
    await page.getByTestId('big-number-value').evaluate((e) => getComputedStyle(e).fontSize),
  ).toBe('300px');
  const value = (await box(page, 'big-number-value'))!;
  expect(value.y + value.height).toBeLessThanOrEqual(callout.y);
  expect(callout.y + callout.height).toBeLessThanOrEqual(996);
});

// The left and right extents, and the bottom, of every text run in the layout.
function textBox(page: Page) {
  return page.getByTestId('layout-big-number').evaluate((root: Element) => {
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

test('bigNumberUnit and its wide twin stay inside the box', async ({ mount, page }) => {
  await stubApi(page, {});
  for (const name of ['bigNumberUnit', 'bigNumberUnitWide', 'bigNumberCallout']) {
    const slots = fitFixture(name).slots as BigNumberSlots;
    const component = await mount(render(bigNumber(slots), 2));
    await page.evaluate(() => document.fonts.ready);
    const drawn = await textBox(page);
    expect(drawn.left, name).toBeGreaterThanOrEqual(120);
    expect(drawn.right, name).toBeLessThanOrEqual(1800);
    expect(drawn.bottom, name).toBeLessThanOrEqual(996);
    await component.unmount();
  }
});

test('screenshot: big-number dark', async ({ mount }) => {
  const component = await mount(
    render(
      bigNumber(
        {
          kicker: 'The cost',
          headline: 'It took us eleven weeks to get a change in front of a customer',
          value: '11 wks',
          caption: 'Median from first commit to the first customer using it',
          detail: 'Eight of our 12 epics.',
          source: 'delivery dashboard, Jan–Dec',
          emphasis: [{ text: 'eleven weeks', tone: 'negative' }],
        },
        'dark',
      ),
    ),
  );
  await expect(component).toHaveScreenshot('layout-big-number-dark.png');
});

test('screenshot: big-number with callout', async ({ mount }) => {
  const component = await mount(
    render(
      bigNumber({
        kicker: 'The cost',
        headline: 'It took us eleven weeks to get a change in front of a customer',
        value: '11 wks',
        caption: 'Median from first commit to the first customer using it',
        detail: 'Eight of our 12 epics.',
        callout: { tone: 'info', text: 'Cut the batch, not the scope.' },
      }),
    ),
  );
  await expect(component).toHaveScreenshot('layout-big-number-callout.png');
});
