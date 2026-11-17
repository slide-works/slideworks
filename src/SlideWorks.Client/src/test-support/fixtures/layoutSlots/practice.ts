import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { PracticeSlots } from '../../../slides/slots/practice';

// One valid practice slide's slots, with every optional slot set.
export const SLOTS: PracticeSlots = {
  kicker: 'Slice it',
  headline: 'Slice by what a customer can do, not by layer',
  do: [{ lead: 'Start from one user action', text: 'and build every layer it needs, thinly.' }],
  comes_back: ['Feedback in days, not quarters.'],
  avoid: ['"API first, UI later."'],
  callout: { tone: 'bad', text: "If a slice can't be demoed, it isn't a slice yet." },
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = [
  'headline',
  'do',
  'comes_back',
  'avoid',
] as const satisfies readonly RequiredKey<PracticeSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<PracticeSlots, typeof REQUIRED>>;
