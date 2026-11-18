// The bars layout's slots (K5). Field names are frozen; the server sets the limits.
import type { CalloutBlock, Emphasis } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';
import type { Reveal, Tone } from './shared';

// One bar, in the order it is drawn. `was` draws a dashed outline at that length.
export type Bar = {
  label: string;
  sub?: string;
  value: number;
  was?: number;
  note?: string;
  tone?: Tone;
  tag?: string;
};

export type BarsSlots = {
  kicker?: string;
  headline: string;
  bars: Bar[];
  unit?: string;
  // 'shown' (the default) or 'hidden', which leaves the numbers off.
  values?: 'shown' | 'hidden';
  // What the dashed outline means; needs a bar with a was.
  wasLabel?: string;
  caption?: string;
  reveal?: Reveal;
  callout?: CalloutBlock;
  emphasis?: Emphasis[];
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'bars',
  'unit',
  'values',
  'wasLabel',
  'caption',
  'reveal',
  'callout',
  'emphasis',
] as const satisfies readonly (keyof BarsSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<BarsSlots, typeof KEYS>>;
