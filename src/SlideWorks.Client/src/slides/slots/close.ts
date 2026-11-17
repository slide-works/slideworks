// The close layout's slots (K5). Field names are frozen; the server sets the limits.
import type { AssertTrue, ListsEveryKey } from './keyChecks';

// headline may carry \n line breaks; it is the only slot that does.
export type CloseSlots = { headline: string; sub?: string; author?: string };

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = ['headline', 'sub', 'author'] as const satisfies readonly (keyof CloseSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<CloseSlots, typeof KEYS>>;
