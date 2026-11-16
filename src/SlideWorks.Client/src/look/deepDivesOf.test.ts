import { describe, expect, it } from 'vitest';
import { sampleDeck } from '../test-support/fixtures/screens';
import { deepDivesOf } from './deepDivesOf';

describe('deepDivesOf', () => {
  it('lists only the slides flagged deep dive, in order, with part, number and headline', () => {
    const slides = sampleDeck.slides.map((slide) => ({
      ...slide,
      deepDive: [1, 2, 6, 13, 16].includes(slide.number),
    }));
    const byNumber = (n: number) => slides.find((slide) => slide.number === n)!;
    expect(deepDivesOf(slides)).toEqual(
      [1, 2, 6, 13, 16].map((n) => ({
        part: byNumber(n).part,
        number: n,
        headline: expectedHeadline(byNumber(n)),
      })),
    );
  });

  it("takes the sample deck's own flags", () => {
    const flagged = sampleDeck.slides.filter((slide) => slide.deepDive);
    expect(flagged.length).toBeGreaterThan(0);
    expect(deepDivesOf(sampleDeck.slides).map((dive) => dive.number)).toEqual(
      flagged.map((slide) => slide.number),
    );
  });

  it('is empty when no slide is flagged', () => {
    expect(deepDivesOf(sampleDeck.slides.map((slide) => ({ ...slide, deepDive: false })))).toEqual(
      [],
    );
  });
});

// The headline each layout carries: a title or divider slide's title, a quote
// slide's quote, every other layout's headline slot.
function expectedHeadline(slide: (typeof sampleDeck.slides)[number]): string {
  const slots = slide.slots as { title?: string; quote?: string; headline?: string };
  if (slide.layout === 'title' || slide.layout === 'divider') return slots.title!;
  if (slide.layout === 'quote') return slots.quote!;
  return slots.headline!;
}
