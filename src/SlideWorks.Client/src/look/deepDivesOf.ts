import { DEFAULT_JOIN_KICKER } from '../slides/layouts/joinKicker';
import { DEFAULT_QA_TITLE } from '../slides/layouts/qaTitle';
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
// of a title or divider slide, a quote slide's quote, a statement slide's statement, a poll slide's question, or a
// qa slide's title.
function headlineOf(slide: DeepDiveSource): string {
  switch (slide.layout) {
    case 'title':
    case 'divider':
      return slide.slots.title;
    case 'quote':
      return slide.slots.quote;
    case 'statement':
      return slide.slots.statement;
    case 'join':
      return slide.slots.kicker ?? DEFAULT_JOIN_KICKER;
    case 'poll':
      return slide.slots.question;
    case 'qa':
      return slide.slots.title ?? DEFAULT_QA_TITLE;
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
