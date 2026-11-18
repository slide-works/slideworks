import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { FlowSlots } from '../../../slides/slots/flow';

// One valid flow slide's slots, with every optional slot set.
export const SLOTS: FlowSlots = {
  kicker: 'The cost',
  headline: 'Every slip we had last year started as a big batch',
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = ['headline'] as const satisfies readonly RequiredKey<FlowSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<FlowSlots, typeof REQUIRED>>;
