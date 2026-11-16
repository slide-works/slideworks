import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { PaletteTokenKey } from '../../api/types';
import { lookViewOf, paletteNamed } from '../../test-support/fixtures/lookViewOf';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { CompareSide, Fill, SlideDeck, Tone } from '../slots';

// Tone panels on the compare layout (SW-2 L4); the sample slide's own content
// is ContentLayouts.spec's.

test.use({ viewport: { width: 1920, height: 1080 } });

type Case = {
  tone: Tone;
  fill?: Fill;
  surface: PaletteTokenKey;
  ink: PaletteTokenKey;
  edge: PaletteTokenKey;
};

const CASES: Case[] = [
  { tone: 'accent', fill: 'soft', surface: 'accent-light', ink: 'accent-dark', edge: 'accent' },
  { tone: 'positive', fill: 'soft', surface: 'good-soft', ink: 'good-ink', edge: 'good' },
  { tone: 'negative', fill: 'soft', surface: 'bad-soft', ink: 'bad-ink', edge: 'bad' },
  { tone: 'inverse', fill: 'soft', surface: 'night', ink: 'night-ink', edge: 'night-2' },
  {
    tone: 'accent',
    fill: 'solid',
    surface: 'panel-accent',
    ink: 'panel-accent-ink',
    edge: 'panel-accent-ink',
  },
  {
    tone: 'positive',
    fill: 'solid',
    surface: 'panel-positive',
    ink: 'panel-positive-ink',
    edge: 'panel-positive-ink',
  },
  {
    tone: 'negative',
    fill: 'solid',
    surface: 'panel-negative',
    ink: 'panel-negative-ink',
    edge: 'panel-negative-ink',
  },
  {
    tone: 'inverse',
    fill: 'solid',
    surface: 'panel-inverse',
    ink: 'panel-inverse-ink',
    edge: 'panel-inverse-ink',
  },
  { tone: 'good', surface: 'good-soft', ink: 'good-ink', edge: 'good' },
  {
    tone: 'bad',
    fill: 'solid',
    surface: 'panel-negative',
    ink: 'panel-negative-ink',
    edge: 'panel-negative-ink',
  },
  { tone: 'info', surface: 'accent-light', ink: 'accent-dark', edge: 'accent' },
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

function side(tone?: Tone, fill?: Fill): CompareSide {
  return {
    label: tone ?? 'Default',
    title: 'Title',
    sub: 'Sub',
    points: ['Point'],
    ...(tone !== undefined ? { tone } : {}),
    ...(fill !== undefined ? { fill } : {}),
  };
}

function compareSlide(number: number, failure: CompareSide, success: CompareSide): RenderableSlide {
  return {
    number,
    layout: 'compare',
    part: null,
    deepDive: false,
    slots: { headline: 'Tones', failure, success },
  };
}

for (const palette of ['cobalt', 'midnight']) {
  test(`compare panels draw every tone and fill in its token pair: ${palette}`, async ({
    mount,
    page,
  }) => {
    const deck = deckIn(palette);
    // Each case on the failure side of one slide and the success side of the next.
    const slides = CASES.map(({ tone, fill }, i) =>
      compareSlide(i + 1, side(tone, fill), side(tone, fill)),
    );
    await mount(
      <div>
        {slides.map((slide) => (
          <SlideRenderer
            key={slide.number}
            slide={slide}
            deck={deck}
            total={slides.length}
            step={0}
          />
        ))}
      </div>,
    );

    for (const testId of ['compare-failure', 'compare-success']) {
      const panels = page.getByTestId(testId);
      await expect(panels).toHaveCount(CASES.length);
      for (const [i, { tone, fill, surface, ink, edge }] of CASES.entries()) {
        const panel = panels.nth(i);
        await expect(panel).toHaveAttribute('data-tone', tone);
        await expect(panel).toHaveAttribute('data-fill', fill ?? 'soft');
        await expect(panel).toHaveCSS('background-color', await computed(page, palette, surface));
        await expect(panel.getByTestId('compare-title')).toHaveCSS(
          'color',
          await computed(page, palette, ink),
        );
        await expect(panel.getByTestId('compare-label')).toHaveCSS(
          'color',
          await computed(page, palette, edge),
        );
        await expect(panel).toHaveCSS(
          'border-top-color',
          await computed(page, palette, fill === 'solid' ? surface : edge),
        );
      }
    }
  });
}

test('compare defaults the failure to negative soft and the success to positive soft', async ({
  mount,
  page,
}) => {
  await mount(
    <SlideRenderer
      slide={compareSlide(1, side(), side())}
      deck={deckIn('plum')}
      total={1}
      step={0}
    />,
  );
  const failure = page.getByTestId('compare-failure');
  await expect(failure).toHaveAttribute('data-tone', 'negative');
  await expect(failure).toHaveAttribute('data-fill', 'soft');
  await expect(failure).toHaveCSS('background-color', await computed(page, 'plum', 'bad-soft'));
  await expect(failure).toHaveCSS('border-top-color', await computed(page, 'plum', 'bad'));
  const success = page.getByTestId('compare-success');
  await expect(success).toHaveAttribute('data-tone', 'positive');
  await expect(success).toHaveAttribute('data-fill', 'soft');
  await expect(success).toHaveCSS('background-color', await computed(page, 'plum', 'good-soft'));
  await expect(success).toHaveCSS('border-top-color', await computed(page, 'plum', 'good'));
});
