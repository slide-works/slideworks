// The quote layout's slots (K5). Field names are frozen; the server sets the limits.
import type { Emphasis, ImageRef } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';
import type { Reveal } from './shared';

// The supporting stat: a short value and the caption that says what it is.
export type QuoteStat = { value: string; caption: string };

// With reveal 'one-by-one' the stat arrives on step 1 and the follow-up after it (on step 1
// when there is no stat). The attribution is the speaker's name when a role is set.
export type QuoteSlots = {
  kicker?: string;
  headline?: string;
  quote: string;
  attribution: string;
  role?: string;
  stat?: QuoteStat;
  follow_up?: string;
  source?: string;
  image?: ImageRef;
  reveal?: Reveal;
  emphasis?: Emphasis[];
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'quote',
  'attribution',
  'role',
  'stat',
  'follow_up',
  'source',
  'image',
  'reveal',
  'emphasis',
] as const satisfies readonly (keyof QuoteSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<QuoteSlots, typeof KEYS>>;
