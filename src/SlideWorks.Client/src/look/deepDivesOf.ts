import type { DeepDive, LayoutName, Slots } from '../slides/slots';

type DeepDiveSource = {
  [L in LayoutName]: {
    number: number;
    part: number | null;
    deepDive: boolean;
    layout: L;
    slots: Slots[L];
  };
}[LayoutName];

// A slide's headline as the divider lists it: its headline slot, or the title
// of a title or divider slide, or a quote slide's quote.
function headlineOf(slide: DeepDiveSource): string {
  switch (slide.layout) {
    case 'title':
    case 'divider':
      return slide.slots.title;
    case 'quote':
      return slide.slots.quote;
    default:
      return slide.slots.headline;
  }
}

// The deck's slides flagged deep_dive, in order (SW-2 L10), for the dividers to list.
export function deepDivesOf(slides: readonly DeepDiveSource[]): DeepDive[] {
  return slides
    .filter((slide) => slide.deepDive)
    .map((slide) => ({ part: slide.part, number: slide.number, headline: headlineOf(slide) }));
}
