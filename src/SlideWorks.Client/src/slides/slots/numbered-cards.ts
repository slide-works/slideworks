// The numbered-cards layout's slots (K5). Field names are frozen; the server sets the limits.
import type { TitleText } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';
import type { Reveal } from './shared';

export type NumberedCardsSlots = {
  kicker?: string;
  headline: string;
  cards: TitleText[];
  reveal?: Reveal;
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'cards',
  'reveal',
] as const satisfies readonly (keyof NumberedCardsSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<NumberedCardsSlots, typeof KEYS>>;
