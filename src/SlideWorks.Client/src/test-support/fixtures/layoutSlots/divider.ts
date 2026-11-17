import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { DividerSlots } from '../../../slides/slots/divider';

// One valid divider slide's slots, with every optional slot set.
export const SLOTS: DividerSlots = {
  title: 'Big batches hide the risk',
  sub: 'Why the quarter-long launch keeps slipping.',
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = ['title'] as const satisfies readonly RequiredKey<DividerSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<DividerSlots, typeof REQUIRED>>;
