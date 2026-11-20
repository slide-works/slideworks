import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { PollSlots } from '../../../slides/slots/poll';

// One valid poll slide's slots, with every optional slot set.
export const SLOTS: PollSlots = {
  question: 'How big was your last merge request?',
  options: ['Under 50 lines', '50 to 200 lines', '200 to 1,000 lines', 'Over 1,000 lines'],
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = [
  'question',
  'options',
] as const satisfies readonly RequiredKey<PollSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<PollSlots, typeof REQUIRED>>;
