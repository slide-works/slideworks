import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { QuoteSlots } from '../../../slides/slots/quote';

// One valid quote slide's slots, with every optional slot set.
export const SLOTS: QuoteSlots = {
  kicker: 'From the retro',
  headline: 'What changed in a quarter',
  quote: "We stopped asking 'is it done?' and started asking 'is it out?'",
  attribution: 'Priya Raman',
  role: 'Head of search, Q3 retro',
  stat: { value: '11 wks', caption: 'From idea to customer, down from 19' },
  follow_up: 'Smaller changes meant fewer surprises on release day.',
  source: 'Search team retro notes, October',
  reveal: 'one-by-one',
  emphasis: [{ text: 'is it out?', tone: 'accent' }],
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = [
  'quote',
  'attribution',
] as const satisfies readonly RequiredKey<QuoteSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<QuoteSlots, typeof REQUIRED>>;
