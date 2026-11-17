import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { QuoteSlots } from '../../../slides/slots/quote';

// One valid quote slide's slots, with every optional slot set.
export const SLOTS: QuoteSlots = {
  kicker: 'From the retro',
  quote: "We stopped asking 'is it done?' and started asking 'is it out?'",
  attribution: 'Search team retro, Q3',
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = [
  'quote',
  'attribution',
] as const satisfies readonly RequiredKey<QuoteSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<QuoteSlots, typeof REQUIRED>>;
