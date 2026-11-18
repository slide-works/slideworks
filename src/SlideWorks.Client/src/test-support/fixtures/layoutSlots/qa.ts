import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { QaSlots } from '../../../slides/slots/qa';

// One valid qa slide's slots, with every optional slot set.
export const SLOTS: QaSlots = { title: 'Your questions' };

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = [] as const satisfies readonly RequiredKey<QaSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<QaSlots, typeof REQUIRED>>;
