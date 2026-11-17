// The big-number layout's slots (K5). Field names are frozen; the server sets the limits.
import type { AssertTrue, ListsEveryKey } from './keyChecks';

export type BigNumberSlots = {
  kicker?: string;
  headline: string;
  value: string;
  caption: string;
  detail?: string;
  source?: string;
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
] as const satisfies readonly (keyof BigNumberSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<BigNumberSlots, typeof KEYS>>;
