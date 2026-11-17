import { createContext, useContext } from 'react';
import type { ReactionEmoji } from '../types';

// One emoji floating up the big screen's right edge: one tap of a reactions batch. id is unique among the bursts a
// provider has given, so a layer can key and retire each one.
export type ScreenBurst = { id: number; emoji: ReactionEmoji };

// The reactions on the big screen (SW-9 Q7, Q9), newest last. Frozen after wave 0.2: the real provider
// (ScreenReactionsLive) and the fixture provider (ScreenReactionsFixture) both give this shape.
export type ScreenReactions = { bursts: ScreenBurst[] };

export const ScreenReactionsContext = createContext<ScreenReactions | null>(null);

// The big screen's reactions, or null outside a provider (the deck page, RenderPage, preview_deck, thumbnails) and for
// a viewer who can't present the deck: the reactions layer then draws nothing.
export function useScreenReactions(): ScreenReactions | null {
  return useContext(ScreenReactionsContext);
}
