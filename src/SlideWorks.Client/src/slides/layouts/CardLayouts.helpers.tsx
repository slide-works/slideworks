import type { Page } from 'playwright/test';
import { paintedExtent, slideBox } from '../../test-support/measure/paintedExtent';
import { lookViewOf } from '../../test-support/fixtures/lookViewOf';
import { sampleDeck } from '../../test-support/fixtures/screens';
import { SlideRenderer, type RenderableSlide } from '../SlideRenderer';
import type { SlideDeck } from '../slots';

// What CardLayouts.spec, NumberedCardsLayout.spec and RulesLayout.spec share:
// the deck, the sample slides and the measure of what a layout draws. No test
// lives here.

export const deck: SlideDeck = {
  title: sampleDeck.title,
  author: sampleDeck.author,
  parts: sampleDeck.parts,
  look: lookViewOf('talk', 'cobalt'),
  deepDives: [],
};

export function sample(number: number): RenderableSlide {
  const slide = sampleDeck.slides.find((s) => s.number === number);
  if (slide === undefined) throw new Error(`sample deck has no slide ${number}`);
  return slide as RenderableSlide;
}

// A plain function, not a component: CT can only mount components from source files.
export function render(slide: RenderableSlide, step = 0) {
  return <SlideRenderer slide={slide} deck={deck} total={sampleDeck.slides.length} step={step} />;
}

export const ACCENT = 'rgb(47, 91, 234)';
export const ACCENT_DARK = 'rgb(30, 58, 138)';
export const ACCENT_LIGHT = 'rgb(228, 234, 253)';

// A palette token from the deck's look, as a computed colour.
export function rgbOf(hex: string) {
  const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16));
  return `rgb(${r}, ${g}, ${b})`;
}

// The extent of what a layout draws (the rules' numerals end at their baseline),
// plus the last list item's box.
export async function measure(page: Page, layoutTestId: string) {
  const extent = await paintedExtent(page, layoutTestId, {
    baselineText: '[data-testid="rules-number"]',
  });
  const lastItem = await slideBox(page, layoutTestId, 'li', true);
  return { ...extent, lastItem };
}
