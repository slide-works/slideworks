// The practice layout's slots (K5). Field names are frozen; the server sets the limits.
import type { CalloutBlock, LeadText } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';

export type PracticeSlots = {
  kicker?: string;
  headline: string;
  do: LeadText[];
  comes_back: string[];
  avoid: string[];
  callout?: CalloutBlock;
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'do',
  'comes_back',
  'avoid',
  'callout',
] as const satisfies readonly (keyof PracticeSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<PracticeSlots, typeof KEYS>>;
