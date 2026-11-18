import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { StatementSlots } from '../../../slides/slots/statement';

// One valid statement slide's slots, with every optional slot set.
export const SLOTS: StatementSlots = {
  kicker: 'The point',
  statement: 'Small batches are how we find out sooner.',
  sub: 'Ship half of it by Friday, then learn from the rest.',
  reveal: 'one-by-one',
  emphasis: [{ text: 'find out sooner', tone: 'accent' }],
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = ['statement'] as const satisfies readonly RequiredKey<StatementSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<StatementSlots, typeof REQUIRED>>;
