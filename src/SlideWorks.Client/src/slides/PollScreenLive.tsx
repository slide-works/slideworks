import type { ReactNode } from 'react';
import { PollScreenProvider } from './PollContext';

export type PollScreenLiveProps = {
  deckId: string;
  // The same flag JoinProvider takes: the viewer may be a person who can present the deck. Anyone else gets null.
  owner: boolean;
  children: ReactNode;
};

// The poll slide's live screen in the player and the audience window (SW-6 V7). For a person who can present, it
// watches the deck's audience, reads GET /api/decks/{id}/polls/{slide} when the presenter's slide changes, and follows
// pollControl, dropping a lower seq.
export function PollScreenLive(props: PollScreenLiveProps) {
  // TODO: no live data yet: every poll slide draws empty tracks. The real provider replaces this.
  return <PollScreenProvider source={null}>{props.children}</PollScreenProvider>;
}
