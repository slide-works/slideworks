import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { OpenTextSlots } from '../../../slides/slots/open-text';

// One valid open-text slide's slots, with every optional slot set.
export const SLOTS: OpenTextSlots = { question: 'What would help your team ship smaller changes?' };

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = ['question'] as const satisfies readonly RequiredKey<OpenTextSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<OpenTextSlots, typeof REQUIRED>>;
