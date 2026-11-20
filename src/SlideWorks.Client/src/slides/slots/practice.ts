// The practice layout's slots (K5). Field names are frozen; the server sets the limits.
import type { CalloutBlock, Emphasis } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';

export type PracticeItem = { lead?: string; text: string };

export type PracticeSlots = {
  kicker?: string;
  headline: string;
  density?: 'regular' | 'compact';
  do: PracticeItem[];
  comes_back?: string[];
  avoid: string[];
  callout?: CalloutBlock;
  emphasis?: Emphasis[];
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'density',
  'do',
  'comes_back',
  'avoid',
  'callout',
  'emphasis',
] as const satisfies readonly (keyof PracticeSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<PracticeSlots, typeof KEYS>>;
