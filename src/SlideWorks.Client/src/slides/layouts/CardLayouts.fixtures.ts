import { legacyFitFixturesOf } from '../../test-support/fixtures/fitFixtures';
import type { Slots } from '../slots';

// The max-content fit fixtures for the numbered-cards, rules and cards layouts,
// shared with the server: src/contracts/fit-fixtures/<layout>.json holds the most text
// the validator accepts at each layout's maximum count, in prose, in Title
// Case and in wide letters (capitals and M and W). SlideWorks.UnitTest's
// FitFixturesTests proves each one is accepted and that one more word in a
// `grow` field is refused, by the fit rule or a character limit as `refusedBy`
// says. Numbered-cards and cards have no fit rule, so every field is at its
// limit. Rules has one (LayoutRules.Rules, SW-124): `estimate` is its height
// for the rules, in px below the body's top. rulesWide (`accepted: false`) is
// past that rule's maximum, and the validator refuses it as it stands.

const CARD_LAYOUTS = ['numbered-cards', 'rules', 'cards'] as const;
type CardLayout = (typeof CARD_LAYOUTS)[number];

export type CardFitFixture = {
  [L in CardLayout]: {
    layout: L;
    estimate?: number;
    accepted?: boolean;
    grow: string[];
    refusedBy: 'fit' | 'length';
    slots: Slots[L];
  };
}[CardLayout];

export const CARD_FIT_FIXTURES = legacyFitFixturesOf(CARD_LAYOUTS) as Record<
  string,
  CardFitFixture
>;

export function cardFitFixture(name: string): CardFitFixture {
  const fixture = CARD_FIT_FIXTURES[name];
  if (fixture === undefined) throw new Error(`fit-fixtures has no card fixture ${name}`);
  return fixture;
}
