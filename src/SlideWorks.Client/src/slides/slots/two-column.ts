// The two-column layout's slots (K5). Field names are frozen; the server sets the limits.
import type { ColumnSlot } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';

export type TwoColumnSlots = {
  kicker?: string;
  headline: string;
  left: ColumnSlot;
  right: ColumnSlot;
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'left',
  'right',
] as const satisfies readonly (keyof TwoColumnSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<TwoColumnSlots, typeof KEYS>>;
