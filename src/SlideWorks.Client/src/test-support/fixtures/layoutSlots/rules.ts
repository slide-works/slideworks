import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { RulesSlots } from '../../../slides/slots/rules';

// One valid rules slide's slots, with every optional slot set.
export const SLOTS: RulesSlots = {
  kicker: 'Make it stick',
  headline: 'Three rules for the first month',
  rules: [{ title: 'Merge to main every day', text: 'Behind a flag if it isn’t ready.' }],
  reveal: 'all',
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = ['headline', 'rules'] as const satisfies readonly RequiredKey<RulesSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<RulesSlots, typeof REQUIRED>>;
