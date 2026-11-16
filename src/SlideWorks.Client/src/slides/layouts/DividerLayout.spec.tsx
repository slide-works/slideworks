import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { PaletteTokenKey } from '../../api/types';
import { lookViewOf, paletteNamed } from '../../test-support/fixtures/lookViewOf';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { DeepDive, SlideDeck } from '../slots';

// The divider's list of its part's deep dives (SW-2 D4, D19): shown when the
// template's chrome.deepDivesOnDivider is on (workshop), never in talk.

test.use({ viewport: { width: 1920, height: 1080 } });

const DEEP_DIVES: DeepDive[] = [
  { part: 1, number: 4, headline: 'Where the time goes' },
  { part: 2, number: 10, headline: 'Story map exercise' },
  { part: 2, number: 12, headline: 'Splitting clinic' },
  { part: 3, number: 14, headline: 'Flag clean-up' },
];

function deckOn(template: string, deepDives: DeepDive[] = DEEP_DIVES): SlideDeck {
  return {
    title: 'Ship smaller',
    author: 'Sam Rivera',
    parts: ['The cost', 'Slice it', 'Ship it'],
    look: lookViewOf(template, 'cobalt'),
    deepDives,
  };
}

function divider(part: number | null): RenderableSlide {
  return {
    number: 7,
    layout: 'divider',
    part,
    deepDive: false,
    slots: { title: 'Slice it thin', sub: 'How to cut work so each piece reaches a customer.' },
  };
}

// A palette token's value as the browser computes a colour.
async function computed(page: Page, key: PaletteTokenKey): Promise<string> {
  return page.evaluate((value) => {
    const probe = document.createElement('div');
    probe.style.color = value;
    document.body.append(probe);
    const colour = getComputedStyle(probe).color;
    probe.remove();
    return colour;
  }, paletteNamed('cobalt').tokens[key]);
}

test("a workshop divider lists its own part's deep dives under the journey", async ({
  mount,
  page,
}) => {
  await mount(<SlideRenderer slide={divider(2)} deck={deckOn('workshop')} total={16} step={0} />);

  const list = page.getByTestId('divider-deep-dives');
  await expect(list).toBeVisible();
  await expect(list).toContainText('Deep dives');
  await expect(list.getByTestId('divider-deep-dive')).toHaveText([
    'Story map exercise',
    'Splitting clinic',
  ]);
  await expect(list.getByTestId('divider-deep-dive').nth(0)).toHaveCSS(
    'border-top-color',
    await computed(page, 'dd'),
  );
  await expect(list.getByTestId('divider-deep-dive').nth(0)).toHaveCSS(
    'color',
    await computed(page, 'dd-on-night'),
  );

  const journey = await page.getByTestId('block-journey').boundingBox();
  const box = await list.boundingBox();
  expect(box!.y).toBeGreaterThanOrEqual(journey!.y + journey!.height);
  expect(box!.y + box!.height).toBeLessThanOrEqual(996);
});

// The worst cases for the list's fit: a two-line title, a sub and a four-part
// journey above it.
function crowded(deepDives: DeepDive[]): { slide: RenderableSlide; deck: SlideDeck } {
  return {
    slide: {
      number: 7,
      layout: 'divider',
      part: 2,
      deepDive: false,
      slots: {
        title: 'Slice the work thin enough to ship every week',
        sub: 'How to cut work so each piece reaches a customer.',
      },
    },
    deck: {
      ...deckOn('workshop', deepDives),
      parts: ['The cost', 'Slice it', 'Ship it', 'Keep going'],
    },
  };
}

function headlineOf(length: number, i: number): string {
  return `${i + 1} ${'Deep dive headline words '.repeat(12)}`.slice(0, length);
}

const WORST_CASES: { name: string; deepDives: DeepDive[] }[] = [
  {
    name: 'three 78-character headlines',
    deepDives: [0, 1, 2].map((i) => ({ part: 2, number: 8 + i, headline: headlineOf(78, i) })),
  },
  {
    name: 'eight 16-character headlines',
    deepDives: [0, 1, 2, 3, 4, 5, 6, 7].map((i) => ({
      part: 2,
      number: 8 + i,
      headline: headlineOf(16, i),
    })),
  },
  {
    name: 'one 266-character quote headline',
    deepDives: [{ part: 2, number: 8, headline: headlineOf(266, 0) }],
  },
];

for (const { name, deepDives } of WORST_CASES) {
  test(`the deep-dive list ends by y 996 with one-line pills: ${name}`, async ({ mount, page }) => {
    const { slide, deck } = crowded(deepDives);
    await mount(<SlideRenderer slide={slide} deck={deck} total={16} step={0} />);

    const list = page.getByTestId('divider-deep-dives');
    await expect(list).toBeVisible();
    await expect(page.getByTestId('divider-title')).toHaveCSS('font-size', '104px');
    const title = await page.getByTestId('divider-title').boundingBox();
    expect(title!.height).toBeGreaterThan(104 * 1.02 * 1.5);

    // Every pill shown, and the '+N more' pill, is one 17 px line high.
    const pills = list.locator('li');
    await expect(pills.first()).toBeVisible();
    for (const pill of await pills.all()) {
      const box = await pill.boundingBox();
      expect(box!.height).toBeLessThan(17 * 2 + 22);
      expect(box!.width).toBeLessThanOrEqual(700);
    }

    // Every deep dive is counted: shown, or in '+N more'.
    const shown = await list.getByTestId('divider-deep-dive').count();
    const more = list.getByTestId('divider-deep-dives-more');
    const hidden = (await more.count()) === 0 ? 0 : Number(/\d+/.exec(await more.innerText())![0]);
    expect(shown + hidden).toBe(deepDives.length);
    expect(shown).toBeLessThanOrEqual(6);

    const box = await list.boundingBox();
    expect(box!.y + box!.height).toBeLessThanOrEqual(996);
  });
}

test("the deep-dive list is labelled by 'Deep dives'", async ({ mount, page }) => {
  await mount(<SlideRenderer slide={divider(2)} deck={deckOn('workshop')} total={16} step={0} />);
  await expect(page.getByRole('list', { name: 'Deep dives' })).toBeVisible();
  await expect(page.getByRole('list', { name: 'Deep dives' }).getByRole('listitem')).toHaveCount(2);
});

test('a talk divider lists no deep dives', async ({ mount, page }) => {
  await mount(<SlideRenderer slide={divider(2)} deck={deckOn('talk')} total={16} step={0} />);
  await expect(page.getByTestId('layout-divider')).toBeVisible();
  await expect(page.getByTestId('divider-deep-dives')).toHaveCount(0);
});

test('a workshop divider whose part has no deep dives lists none', async ({ mount, page }) => {
  await mount(
    <SlideRenderer
      slide={divider(2)}
      deck={deckOn(
        'workshop',
        DEEP_DIVES.filter((d) => d.part !== 2),
      )}
      total={16}
      step={0}
    />,
  );
  await expect(page.getByTestId('layout-divider')).toBeVisible();
  await expect(page.getByTestId('divider-deep-dives')).toHaveCount(0);
});

test('a divider outside any part lists no deep dives', async ({ mount, page }) => {
  await mount(
    <SlideRenderer
      slide={divider(null)}
      deck={deckOn('workshop', [{ part: null, number: 3, headline: 'Unparted' }])}
      total={16}
      step={0}
    />,
  );
  await expect(page.getByTestId('layout-divider')).toBeVisible();
  await expect(page.getByTestId('divider-deep-dives')).toHaveCount(0);
});

test('a deep-dive headline is plain text, never markup', async ({ mount, page }) => {
  await mount(
    <SlideRenderer
      slide={divider(2)}
      deck={deckOn('workshop', [{ part: 2, number: 9, headline: '<b>not bold</b>' }])}
      total={16}
      step={0}
    />,
  );
  await expect(page.getByTestId('divider-deep-dive')).toHaveText('<b>not bold</b>');
  await expect(page.getByTestId('divider-deep-dive').locator('b')).toHaveCount(0);
});

test('screenshot: workshop divider with deep dives', async ({ mount }) => {
  const component = await mount(
    <SlideRenderer slide={divider(2)} deck={deckOn('workshop')} total={16} step={0} />,
  );
  await expect(component).toHaveScreenshot('divider-deep-dives.png');
});
