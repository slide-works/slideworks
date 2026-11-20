// The join layout's slots (K5). Field names are frozen; the server sets the limits.
import type { AssertTrue, ListsEveryKey } from './keyChecks';

// The join slide's slots (SW-5 J6). The code and the QR come from the live session, never from the slots.
export type JoinSlots = { kicker?: string; count?: 'shown' | 'hidden' };

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = ['kicker', 'count'] as const satisfies readonly (keyof JoinSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<JoinSlots, typeof KEYS>>;
