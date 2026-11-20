// The bullets layout's slots (K5). Field names are frozen; the server sets the limits.
import type { BulletItem, CalloutBlock, Emphasis } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';
import type { Reveal } from './shared';

export type BulletsSlots = {
  kicker?: string;
  headline: string;
  items: BulletItem[];
  // 'markers' (the default) draws a square; 'numbered' draws 1, 2, 3.
  style?: 'markers' | 'numbered';
  reveal?: Reveal;
  callout?: CalloutBlock;
  emphasis?: Emphasis[];
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'items',
  'style',
  'reveal',
  'callout',
  'emphasis',
] as const satisfies readonly (keyof BulletsSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<BulletsSlots, typeof KEYS>>;
