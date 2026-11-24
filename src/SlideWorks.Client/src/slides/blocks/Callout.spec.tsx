import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { PaletteTokenKey } from '../../api/types';
import { lookViewOf, paletteNamed } from '../../test-support/fixtures/lookViewOf';
import { SlideRenderer } from '../SlideRenderer';
import type { CalloutBlock, Fill, SlideDeck, Tone } from '../slots';
import { Callout } from './Callout';

test('callout tones', async ({ mount, page }) => {
  await mount(
    <div style={{ width: 800 }}>
      <Callout tone="info" text="An info callout" />
      <Callout tone="good" text="A good callout" />
      <Callout tone="bad" text="A bad callout" />
    </div>,
  );

  const callouts = page.getByTestId('block-callout');
  await expect(callouts).toHaveText(['An info callout', 'A good callout', 'A bad callout']);
  await expect(callouts.nth(0)).toHaveAttribute('data-tone', 'info');
  await expect(callouts.nth(0)).toHaveCSS('background-color', 'rgb(228, 234, 253)');
  await expect(callouts.nth(0)).toHaveCSS('color', 'rgb(30, 58, 138)');
  await expect(callouts.nth(0)).toHaveCSS('box-shadow', 'none');

  await expect(callouts.nth(1)).toHaveAttribute('data-tone', 'good');
  await expect(callouts.nth(1)).toHaveCSS('background-color', 'rgb(220, 252, 231)');
  await expect(callouts.nth(1)).toHaveCSS('box-shadow', 'rgb(21, 128, 61) 6px 0px 0px 0px inset');

  await expect(callouts.nth(2)).toHaveAttribute('data-tone', 'bad');
  await expect(callouts.nth(2)).toHaveCSS('background-color', 'rgb(253, 236, 236)');
  await expect(callouts.nth(2)).toHaveCSS('box-shadow', 'rgb(220, 38, 38) 6px 0px 0px 0px inset');
});

test('callout text is plain text, never markup', async ({ mount, page }) => {
  await mount(<Callout tone="info" text="<b>not bold</b> **nor this**" />);
  await expect(page.getByTestId('block-callout')).toHaveText('<b>not bold</b> **nor this**');
  await expect(page.getByTestId('block-callout').locator('b')).toHaveCount(0);
});

// Every tone and fill, and each alias, with the surface and ink token it draws in (SW-2 L4).
const CASES: { tone: Tone; fill?: Fill; surface: PaletteTokenKey; ink: PaletteTokenKey }[] = [
  { tone: 'accent', fill: 'soft', surface: 'accent-light', ink: 'accent-dark' },
  { tone: 'positive', fill: 'soft', surface: 'good-soft', ink: 'good-ink' },
  { tone: 'negative', fill: 'soft', surface: 'bad-soft', ink: 'bad-ink' },
  { tone: 'inverse', fill: 'soft', surface: 'night', ink: 'night-ink' },
  { tone: 'accent', fill: 'solid', surface: 'panel-accent', ink: 'panel-accent-ink' },
  { tone: 'positive', fill: 'solid', surface: 'panel-positive', ink: 'panel-positive-ink' },
  { tone: 'negative', fill: 'solid', surface: 'panel-negative', ink: 'panel-negative-ink' },
  { tone: 'inverse', fill: 'solid', surface: 'panel-inverse', ink: 'panel-inverse-ink' },
  { tone: 'good', surface: 'good-soft', ink: 'good-ink' },
  { tone: 'bad', fill: 'solid', surface: 'panel-negative', ink: 'panel-negative-ink' },
  { tone: 'info', surface: 'accent-light', ink: 'accent-dark' },
];

// A palette token's value as the browser computes a colour.
async function computed(page: Page, palette: string, key: PaletteTokenKey): Promise<string> {
  return page.evaluate((value) => {
    const probe = document.createElement('div');
    probe.style.color = value;
    document.body.append(probe);
    const colour = getComputedStyle(probe).color;
    probe.remove();
    return colour;
  }, paletteNamed(palette).tokens[key]);
}

function deckIn(palette: string): SlideDeck {
  return {
    title: 'Tones',
    author: null,
    parts: [],
    look: lookViewOf('workshop', palette),
    deepDives: [],
  };
}

for (const palette of ['cobalt', 'midnight']) {
  test(`callout draws every tone and fill in its token pair: ${palette}`, async ({
    mount,
    page,
  }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });
    const deck = deckIn(palette);
    await mount(
      <div>
        {CASES.map(({ tone, fill }, i) => {
          const callout: CalloutBlock =
            fill === undefined ? { tone, text: tone } : { tone, fill, text: tone };
          return (
            <SlideRenderer
              key={i}
              slide={{
                number: i + 1,
                layout: 'bullets',
                part: null,
                deepDive: false,
                slots: { headline: 'Tones', items: ['One'], callout },
              }}
              deck={deck}
              total={CASES.length}
              step={0}
            />
          );
        })}
      </div>,
    );

    const callouts = page.getByTestId('block-callout');
    await expect(callouts).toHaveCount(CASES.length);
    for (const [i, { tone, fill, surface, ink }] of CASES.entries()) {
      const callout = callouts.nth(i);
      await expect(callout).toHaveAttribute('data-tone', tone);
      await expect(callout).toHaveAttribute('data-fill', fill ?? 'soft');
      await expect(callout).toHaveCSS('background-color', await computed(page, palette, surface));
      await expect(callout).toHaveCSS('color', await computed(page, palette, ink));
    }
  });
}

test('screenshot: callout tones', async ({ mount }) => {
  const component = await mount(
    <div style={{ width: 800, padding: 20, background: '#f7f8fa' }}>
      <Callout tone="info" text="An info callout" />
      <Callout tone="good" text="A good callout" />
      <Callout tone="bad" text="A bad callout" />
    </div>,
  );
  await expect(component).toHaveScreenshot('callout-tones.png');
});
