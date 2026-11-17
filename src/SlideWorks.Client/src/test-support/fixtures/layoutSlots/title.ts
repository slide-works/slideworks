import type {
  AssertTrue,
  RequiredKey,
  ListsEveryRequiredKey,
} from '../../../slides/slots/keyChecks';
import type { TitleSlots } from '../../../slides/slots/title';

// One valid title slide's slots, with every optional slot set.
export const SLOTS: TitleSlots = {
  kicker: 'Team offsite · 2026',
  title: 'Ship smaller',
  subtitle: 'Why small batches beat big plans, and how to start on Monday',
  tagline: 'One change a day beats one launch a quarter.',
  author: 'Sam Rivera',
  affiliation: 'Product Engineering',
  stamp: 'Draft',
};

// The slot keys the layout can't do without. `satisfies` rejects a key that is
// optional or unknown, and RequiredComplete rejects a required key left out.
export const REQUIRED = ['title'] as const satisfies readonly RequiredKey<TitleSlots>[];
export type RequiredComplete = AssertTrue<ListsEveryRequiredKey<TitleSlots, typeof REQUIRED>>;
