import type { Page } from 'playwright/test';
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

export type Box = { left: number; top: number; right: number; bottom: number };

// What the layout draws, in canvas px from the slide's top left: every run of
// text and every painted box (a background, shadow or border) inside the
// layout, as ContentLayouts.spec measures it. Layout boxes that only stretch to
// fill the body, such as the body itself or a list, are left out. Also the top
// of every element box, the last list item's box, the body's box and the
// footer's top.
export async function measure(page: Page, layoutTestId: string) {
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
    // A 20 px/1 kicker's glyphs rise a little above its line box; the boxes don't.
    let boxTop = Infinity;
    for (const element of root.querySelectorAll('*')) {
      const rect = element.getBoundingClientRect();
      if (rect.width > 0 || rect.height > 0) boxTop = Math.min(boxTop, rel(rect).top);
      const style = getComputedStyle(element);
      const painted =
        style.backgroundColor !== 'rgba(0, 0, 0, 0)' ||
        style.boxShadow !== 'none' ||
        parseFloat(style.borderTopWidth) > 0;
      if (painted) add(rect);
    }
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
      // A rule's numeral is 150 px on a 0.85 line, so its text rect (the font's
      // whole ascent and descent, about 189 px) runs some 30 px past its line
      // box. Digits have no descenders, so its ink ends at the baseline: a
      // zero-height probe on the baseline measures that instead, and the
      // numeral's left and right come from its own box.
      if (node.parentElement?.matches('[data-testid="rules-number"]')) {
        const probe = document.createElement('span');
        probe.style.display = 'inline-block';
        probe.style.height = '0';
        node.parentElement.append(probe);
        const baseline = rel(probe.getBoundingClientRect()).bottom;
        probe.remove();
        const numeral = rel(node.parentElement.getBoundingClientRect());
        drawn.left = Math.min(drawn.left, numeral.left);
        drawn.right = Math.max(drawn.right, numeral.right);
        drawn.bottom = Math.max(drawn.bottom, baseline);
        continue;
      }
      const range = document.createRange();
      range.selectNodeContents(node);
      for (const rect of range.getClientRects()) add(rect);
    }
    const items = root.querySelectorAll('li');
    const lastItem = rel(items[items.length - 1]!.getBoundingClientRect());
    const body = rel(root.querySelector('.sw-body')!.getBoundingClientRect());
    const footer = document.querySelector('[data-testid="slide-footer"]')!.getBoundingClientRect();
    return { drawn, boxTop, body, lastItem, footerTop: footer.top - slide.top };
  });
}
