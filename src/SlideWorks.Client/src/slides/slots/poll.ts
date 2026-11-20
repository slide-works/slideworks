// The poll layout's slots (K5). Field names are frozen; the server sets the limits.
import type { AssertTrue, ListsEveryKey } from './keyChecks';

// The poll slide's slots (SW-6 V1): the question and 2 to 8 options, known by position. The poll's settings (pick,
// results, correct, opens) are never slots: they live in the poll's own row, so the correct answer never reaches a viewer.
export type PollSlots = { question: string; options: string[] };

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = ['question', 'options'] as const satisfies readonly (keyof PollSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<PollSlots, typeof KEYS>>;
