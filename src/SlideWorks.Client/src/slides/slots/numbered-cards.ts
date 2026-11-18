// The numbered-cards layout's slots (K5). Field names are frozen; the server sets the limits.
import type { CalloutBlock, Emphasis, PanelFill, TitleText } from './blocks';
import type { AssertTrue, ListsEveryKey } from './keyChecks';
import type { Reveal, Tone } from './shared';

// A numbered card: a title and text, and optionally a tone, a fill and a pin
// to highlight it (fill is ignored without a tone).
export type NumberedCard = TitleText & { tone?: Tone; fill?: PanelFill; pin?: string };

export type NumberedCardsSlots = {
  kicker?: string;
  headline: string;
  cards: NumberedCard[];
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
] as const satisfies readonly (keyof NumberedCardsSlots)[];
export type KeysComplete = AssertTrue<ListsEveryKey<NumberedCardsSlots, typeof KEYS>>;
