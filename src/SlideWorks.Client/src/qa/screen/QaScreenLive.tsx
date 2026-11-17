import type { ReactNode } from 'react';
import { QaScreenProvider } from './QaScreenContext';

export type QaScreenLiveProps = {
  deckId: string;
  // The same flag PollScreenLive and JoinProvider take: the viewer may be a person who can present the deck
  // (DeckPlayView.isOwner). Anyone else gets null, and the provider makes no call.
  owner: boolean;
  children: ReactNode;
};

// The big screen's questions in the player and the audience window (SW-9 Q9), inside JoinProvider.
// TODO: always null (no live data); the real provider watches the deck's audience for a person who can present, reads
// GET /api/decks/{id}/qa while live and follows questionControl, dropping a lower seq.
export function QaScreenLive({ children }: QaScreenLiveProps) {
  return <QaScreenProvider screen={null}>{children}</QaScreenProvider>;
}
