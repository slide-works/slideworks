import { test, expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright/test';
import type { PaletteTokenKey } from '../../api/types';
import { lookViewOf, paletteNamed } from '../../test-support/fixtures/lookViewOf';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { Card, Fill, SlideDeck, Tone } from '../slots';

// Tone panels on the cards layout (SW-2 L4), rendered through SlideRenderer so
// the deck's look sits on the slide root.

test.use({ viewport: { width: 1920, height: 1080 } });

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

function cardsSlide(number: number, cards: Card[]): RenderableSlide {
  return {
    number,
    layout: 'cards',
    part: null,
    deepDive: false,
    slots: { headline: 'Tones', cards },
  };
}

function card(tone?: Tone, fill?: Fill): Card {
  return {
    title: tone ?? 'Plain',
    text: 'Text',
    chips: [],
    ...(tone !== undefined ? { tone } : {}),
    ...(fill !== undefined ? { fill } : {}),
  };
}

for (const palette of ['cobalt', 'midnight']) {
  test(`cards draw every tone and fill in its token pair: ${palette}`, async ({ mount, page }) => {
    const deck = deckIn(palette);
    // Three cards a slide, as the layout allows.
    const slides: RenderableSlide[] = [];
    for (let i = 0; i < CASES.length; i += 3) {
      slides.push(
        cardsSlide(
          slides.length + 1,
          CASES.slice(i, i + 3).map(({ tone, fill }) => card(tone, fill)),
        ),
      );
    }
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

    const cards = page.getByTestId('cards-card');
    await expect(cards).toHaveCount(CASES.length);
    for (const [i, { tone, fill, surface, ink }] of CASES.entries()) {
      await expect(cards.nth(i)).toHaveAttribute('data-tone', tone);
      await expect(cards.nth(i)).toHaveAttribute('data-fill', fill ?? 'soft');
      await expect(cards.nth(i)).toHaveCSS(
        'background-color',
        await computed(page, palette, surface),
      );
      await expect(cards.nth(i).getByTestId('cards-title')).toHaveCSS(
        'color',
        await computed(page, palette, ink),
      );
    }
  });
}

test('a card with no tone is the plain card', async ({ mount, page }) => {
  await mount(
    <SlideRenderer
      slide={cardsSlide(1, [card(), card('accent')])}
      deck={deckIn('ember')}
      total={1}
      step={0}
    />,
  );
  const plain = page.getByTestId('cards-card').nth(0);
  await expect(plain).not.toHaveAttribute('data-tone');
  await expect(plain).toHaveCSS('background-color', await computed(page, 'ember', 'card'));
  await expect(plain.getByTestId('cards-title')).toHaveCSS(
    'color',
    await computed(page, 'ember', 'ink'),
  );
  await expect(plain.getByTestId('cards-text')).toHaveCSS(
    'color',
    await computed(page, 'ember', 'muted'),
  );
});

test('the cards of a row stay one height whatever their text', async ({ mount, page }) => {
  const long = {
    ...card('positive', 'solid'),
    text: 'A longer text that wraps onto several lines. '.repeat(3),
  };
  await mount(
    <SlideRenderer
      slide={cardsSlide(1, [card('accent'), long, card()])}
      deck={deckIn('cobalt')}
      total={1}
      step={0}
    />,
  );
  const heights = await page
    .getByTestId('cards-card')
    .evaluateAll((cards) => cards.map((c) => c.getBoundingClientRect().height));
  expect(new Set(heights).size).toBe(1);
});

// The mockup's panels slide (section 06): the cards with fill solid, in each palette.
const PANELS: Card[] = [
  {
    title: 'Feedback every Friday',
    text: 'From someone who uses it, not from a sign-off meeting.',
    chips: [],
    tone: 'positive',
    fill: 'solid',
  },
  {
    title: 'Holding a slice back',
    text: '"It isn\'t complete yet" is how a slice turns back into a batch.',
    chips: [],
    tone: 'negative',
    fill: 'solid',
  },
  {
    title: 'Done means used',
    text: "A customer can do something today that they couldn't do yesterday.",
    chips: [],
    tone: 'accent',
    fill: 'solid',
  },
];

const PANELS_SLIDE: RenderableSlide = {
  number: 1,
  layout: 'cards',
  part: null,
  deepDive: false,
  slots: { kicker: 'Tone panels', headline: 'What a slice gives back', cards: PANELS },
};

for (const palette of ['cobalt', 'ember', 'plum', 'midnight']) {
  test(`screenshot: panels in ${palette}`, async ({ mount }) => {
    const component = await mount(
      <SlideRenderer slide={PANELS_SLIDE} deck={deckIn(palette)} total={1} step={0} />,
    );
    await expect(component).toHaveScreenshot(`panels-${palette}.png`);
  });
}
