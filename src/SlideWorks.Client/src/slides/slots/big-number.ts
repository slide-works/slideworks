// The big-number layout's slots (K5). Field names are frozen; the server sets the limits.
import type { CalloutBlock, Emphasis } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';
import type { Reveal } from './shared';

export type BigNumberSlots = {
  kicker?: string;
  headline: string;
  value: string;
  caption: string;
  detail?: string;
  source?: string;
  reveal?: Reveal;
  callout?: CalloutBlock;
  emphasis?: Emphasis[];
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'value',
  'caption',
  'detail',
  'source',
  'reveal',
  'callout',
  'emphasis',
] as const satisfies readonly (keyof BigNumberSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<BigNumberSlots, typeof KEYS>>;
