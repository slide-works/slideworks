// The bars layout's slots (K5). Field names are frozen; the server sets the limits.
// TODO: only the heading's slots; the layout's own slots join them when the layout is built.
import type { AssertTrue, ListsEveryKey } from './keyChecks';

export type BarsSlots = { kicker?: string; headline: string };

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = ['kicker', 'headline'] as const satisfies readonly (keyof BarsSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<BarsSlots, typeof KEYS>>;
