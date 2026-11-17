import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { JoinSlots } from '../../../slides/slots/join';

// One valid join slide's slots, with every optional slot set.
export const SLOTS: JoinSlots = { kicker: 'Join on your phone', count: 'shown' };

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = [] as const satisfies readonly RequiredKey<JoinSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<JoinSlots, typeof REQUIRED>>;
