import { createContext, useContext, type ReactNode } from 'react';
import type { LiveView } from './liveView';
import { NO_LIVE_SESSION, useLiveSession, type LiveSession } from './useLiveSession';

const LiveSessionContext = createContext<LiveSession>(NO_LIVE_SESSION);

// Gives everything under it one live session for the deck, so the chip, the join slide and the join bar agree.
// initial is the owner's LiveView, or null for anyone else.
export function LiveSessionProvider({
  deckId,
  initial,
  children,
}: {
  deckId: string;
  initial: LiveView | null;
  children: ReactNode;
}) {
  const session = useLiveSession(deckId, initial);
  return <LiveSessionContext value={session}>{children}</LiveSessionContext>;
}

// The live session of the nearest LiveSessionProvider; with none, one that is not the owner's.
export function useLiveSessionContext(): LiveSession {
  return useContext(LiveSessionContext);
}
