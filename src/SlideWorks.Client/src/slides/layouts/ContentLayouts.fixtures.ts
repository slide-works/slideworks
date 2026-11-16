import fitFixturesJson from '@contracts/fit-fixtures.json' with { type: 'json' };
import type { Slots } from '../slots';

// The max-content fit fixtures for the bullets, two-column, practice and
// compare layouts, shared with the server: src/contracts/fit-fixtures.json
// holds the most Title Case text SlideFit (BODY_EM 0.56) accepts in each case,
// and SlideWorks.UnitTest's FitFixturesTests proves each one is accepted and
// refused with one more word. `estimate` is LayoutRules' height for the body's
// tallest column, in px below the body's top.

const CONTENT_LAYOUTS = ['bullets', 'two-column', 'practice', 'compare'] as const;
type ContentLayout = (typeof CONTENT_LAYOUTS)[number];

export type FitFixture = {
  [L in ContentLayout]: { layout: L; estimate: number; grow: string[]; slots: Slots[L] };
}[ContentLayout];

// The file also holds the card and data layouts' fixtures (CardLayouts.fixtures.ts
// and DataLayouts.fixtures.ts).
export const FIT_FIXTURES = Object.fromEntries(
  Object.entries(fitFixturesJson).filter(([, fixture]) =>
    (CONTENT_LAYOUTS as readonly string[]).includes(fixture.layout),
  ),
) as Record<string, FitFixture>;
