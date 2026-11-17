import { createContext, useContext } from 'react';
import type { ReactionEmoji } from '../types';

// The phone's reactions (Q7, Q8). Frozen: G1 fills it in behind ReactionsLive, and the row builds against
// PhoneReactionsFixture.
export type PhoneReactions = {
  // Whether the room's reactions are on for the slide on screen (room.reactions). The row is absent when false.
  enabled: boolean;
  // Sends one tap. Resolves when the call has ended, whatever the outcome: cooling and error say how it went.
  tap(emoji: ReactionEmoji): Promise<void>;
  // A 429 came back: true for the rest of the 5 seconds, and the row is greyed.
  cooling: boolean;
  // The latest failed tap's code from the server ("reactions_off") or the client's own text for a call that never
  // reached it. Null when nothing has failed since the last tap that went through.
  error: string | null;
};

// Holds the reactions the provider above gives: ReactionsLive in the app, PhoneReactionsFixture in specs.
export const PhoneReactionsContext = createContext<PhoneReactions | null>(null);

export function usePhoneReactions(): PhoneReactions {
  const reactions = useContext(PhoneReactionsContext);
  if (reactions === null)
    throw new Error('usePhoneReactions needs a ReactionsLive or a PhoneReactionsFixture above it.');
  return reactions;
}
