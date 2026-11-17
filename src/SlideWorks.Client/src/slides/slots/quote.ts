// The quote layout's slots (K5). Field names are frozen; the server sets the limits.
import type { AssertTrue, ListsEveryKey } from './keyChecks';

export type QuoteSlots = { kicker?: string; quote: string; attribution: string };

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'quote',
  'attribution',
] as const satisfies readonly (keyof QuoteSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<QuoteSlots, typeof KEYS>>;
