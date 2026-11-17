import { legacyFitFixturesOf } from '../../test-support/fixtures/fitFixtures';
import type { Slots } from '../slots';

// The max-content fit fixtures for the bullets, two-column, practice and
// compare layouts, shared with the server: src/contracts/fit-fixtures/<layout>.json
// holds the most Title Case text SlideFit (BODY_EM 0.56) accepts in each case,
// and SlideWorks.UnitTest's FitFixturesTests proves each one is accepted and
// refused with one more word. `estimate` is LayoutRules' height for the body's
// tallest column, in px below the body's top.

const CONTENT_LAYOUTS = ['bullets', 'two-column', 'practice', 'compare'] as const;
type ContentLayout = (typeof CONTENT_LAYOUTS)[number];

export type FitFixture = {
  [L in ContentLayout]: { layout: L; estimate: number; grow: string[]; slots: Slots[L] };
}[ContentLayout];

// Only the fixtures fit-fixtures.json held before it was split: a later one is
// drawn with fitFixture or fitFixturesOf (test-support/fixtures/fitFixtures.ts).
export const FIT_FIXTURES = legacyFitFixturesOf(CONTENT_LAYOUTS) as Record<string, FitFixture>;
