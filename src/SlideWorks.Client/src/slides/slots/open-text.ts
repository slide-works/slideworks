// The open-text layout's slots (K5). Field names are frozen; the server sets the limits.
import type { AssertTrue, ListsEveryKey } from './keyChecks';

// The open-text slide's one slot (SW-8): the question, up to 120 characters. The answers come from the live session,
// never from the slots.
export type OpenTextSlots = { question: string };

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = ['question'] as const satisfies readonly (keyof OpenTextSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<OpenTextSlots, typeof KEYS>>;
