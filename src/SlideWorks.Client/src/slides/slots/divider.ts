// The divider layout's slots (K5). Field names are frozen; the server sets the limits.
import type { AssertTrue, ListsEveryKey } from './keyChecks';

export type DividerSlots = { title: string; sub?: string };

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = ['title', 'sub'] as const satisfies readonly (keyof DividerSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<DividerSlots, typeof KEYS>>;
