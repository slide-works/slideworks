// The title layout's slots (K5). Field names are frozen; the server sets the limits.
import type { AssertTrue, ListsEveryKey } from './keyChecks';

export type TitleSlots = {
  kicker?: string;
  title: string;
  subtitle?: string;
  tagline?: string;
  author?: string;
  affiliation?: string;
  stamp?: string;
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'title',
  'subtitle',
  'tagline',
  'author',
  'affiliation',
  'stamp',
] as const satisfies readonly (keyof TitleSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<TitleSlots, typeof KEYS>>;
