// The levels layout's slots (K5). Field names are frozen; the server sets the limits.
import type { CalloutBlock, ColumnSlot, Emphasis, FactsBlock, LevelsBlock } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';

export type LevelsSlots = {
  kicker?: string;
  headline: string;
  // The ladder of 2–7 levels, top rung first.
  levels: LevelsBlock;
  // Exactly one of items, text or image: the same column two-column uses.
  main: ColumnSlot;
  // 2–4 tiles under the main column.
  facts?: FactsBlock;
  // The side the ladder is on: 'right' (the default) or 'left'.
  side?: 'right' | 'left';
  callout?: CalloutBlock;
  emphasis?: Emphasis[];
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'levels',
  'main',
  'facts',
  'side',
  'callout',
  'emphasis',
] as const satisfies readonly (keyof LevelsSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<LevelsSlots, typeof KEYS>>;
