// The cards layout's slots (K5). Field names are frozen; the server sets the limits.
import type { AssertTrue, ListsEveryKey } from './keyChecks';
import type { CalloutBlock, Emphasis, PanelFill } from './blocks';
import type { Reveal, Tone } from './shared';

// A card with no tone is the plain card. `icon` is a name from icons.json and
// `pin` a short label in the card's corner; a fill without a tone is ignored.
export type Card = {
  badge?: string;
  icon?: string;
  title: string;
  text: string;
  chips: string[];
  tone?: Tone;
  fill?: PanelFill;
  pin?: string;
};

export type CardsSlots = {
  kicker?: string;
  headline: string;
  cards: Card[];
  reveal?: Reveal;
  callout?: CalloutBlock;
  emphasis?: Emphasis[];
};

// Every top-level slot key, at runtime. `satisfies` stops a key that isn't in
// the type, and KeysComplete stops a key of the type from being left out.
export const KEYS = [
  'kicker',
  'headline',
  'cards',
  'reveal',
  'callout',
  'emphasis',
] as const satisfies readonly (keyof CardsSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<CardsSlots, typeof KEYS>>;
