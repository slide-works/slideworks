import { useMemo, type ReactNode } from 'react';
import { useJoin } from '../../slides/JoinContext';
import { ScreenReactionsContext } from './useScreenReactions';

export type ScreenReactionsLiveProps = {
  deckId: string;
  // The same flag PollScreenLive and JoinProvider take: the viewer may be a person who can present the deck
  // (DeckPlayView.isOwner). Anyone else gets null, and the provider makes no call.
  owner: boolean;
  children: ReactNode;
};

// The big screen's reactions in the player and the audience window (SW-9 Q7, Q9), inside JoinProvider. Null for a
// viewer who can't present the deck and outside a JoinProvider.
// TODO: never any bursts; the real provider watches the deck's audience and turns each reactions event into bursts.
export function ScreenReactionsLive({ deckId, owner, children }: ScreenReactionsLiveProps) {
  const inJoin = useJoin() !== null;
  const listening = owner && deckId !== '' && inJoin;
  const value = useMemo(() => (listening ? { bursts: [] } : null), [listening]);
  return <ScreenReactionsContext value={value}>{children}</ScreenReactionsContext>;
}
