import fitFixturesJson from '@contracts/fit-fixtures.json' with { type: 'json' };
import type { Slots } from '../slots';

// The max-content fit fixtures for the bullets, two-column, practice and
// compare layouts, shared with the server: src/contracts/fit-fixtures.json
// holds the most Title Case text SlideFit (BODY_EM 0.56) accepts in each case,
// and SlideWorks.UnitTest's FitFixturesTests proves each one is accepted and
// refused with one more word. `estimate` is LayoutRules' height for the body's
// tallest column, in px below the body's top.

type ContentLayout = 'bullets' | 'two-column' | 'practice' | 'compare';

export type FitFixture = {
  [L in ContentLayout]: { layout: L; estimate: number; grow: string[]; slots: Slots[L] };
}[ContentLayout];

export const FIT_FIXTURES = fitFixturesJson as Record<string, FitFixture>;

const PROSE =
  'The team ships a small slice of work to customers every week, and the ' +
  'feedback arrives in days rather than quarters, so the risk shows up early ' +
  'while it is still cheap to change course and nobody has to guess how far ' +
  'along the work really is.';

// Ordinary prose of exactly `length` characters, starting with a capital.
export function fill(length: number): string {
  let text = '';
  while (text.length < length) text += (text ? ' ' : '') + PROSE;
  return text.slice(0, length).trimEnd().padEnd(length, 'e');
}
