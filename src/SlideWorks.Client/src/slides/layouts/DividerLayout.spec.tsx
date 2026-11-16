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
