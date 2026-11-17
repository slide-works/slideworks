// The cards layout's slots (K5). Field names are frozen; the server sets the limits.
import type { AssertTrue, ListsEveryKey } from './keyChecks';
import type { Fill, Reveal, Tone } from './shared';

// A card with no tone is the plain card.
export type Card = {
  badge?: string;
  title: string;
  text: string;
  chips: string[];
  tone?: Tone;
  fill?: Fill;
};

export type CardsSlots = { kicker?: string; headline: string; cards: Card[]; reveal?: Reveal };

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'cards',
  'reveal',
] as const satisfies readonly (keyof CardsSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<CardsSlots, typeof KEYS>>;
