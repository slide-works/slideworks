// The qa layout's slots (K5). Field names are frozen; the server sets the limits.
import type { AssertTrue, ListsEveryKey } from './keyChecks';

// The Q&A slide's one slot (SW-9 Q1): a title of up to 80 characters; left out, the slide draws "Questions from the room".
// The questions come from the live session, never from the slots.
export type QaSlots = { title?: string };

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = ['title'] as const satisfies readonly (keyof QaSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<QaSlots, typeof KEYS>>;
