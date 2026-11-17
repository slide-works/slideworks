import { createContext, useContext, useMemo, type ReactNode } from 'react';
import type { JoinView } from '../api/types';
import { LiveSessionProvider, useLiveSessionContext } from '../live/LiveSessionProvider';
import { idleLiveView } from '../live/useLiveSession';

// What the join slide and the join bar read (SW-5 J6): the deck's join, whether a session is open and how many have
// joined. Slides never fetch these themselves.
export type JoinValue = { join: JoinView; live: boolean; joinedCount: number };

const JoinContext = createContext<JoinValue | null>(null);

// The deck's join, or null outside a JoinProvider.
export function useJoin(): JoinValue | null {
  return useContext(JoinContext);
}

function JoinValueProvider({ join, children }: { join: JoinView; children: ReactNode }) {
  const session = useLiveSessionContext();
  const value = useMemo<JoinValue>(
    () =>
      session.liveView === null
        ? { join, live: join.live, joinedCount: 0 }
        : { join, live: session.live, joinedCount: session.joinedCount },
    [join, session.liveView, session.live, session.joinedCount],
  );
  return <JoinContext value={value}>{children}</JoinContext>;
}

// Provides the join to the slides under it, and the deck's live session to the chip. The owner's live state comes from
// the live session and anyone else's from join.live, which the page re-reads on liveChanged. owner means the viewer may
// be the owner; the live session confirms it, and shows no chip until it does. It is false for a viewer who can't be
// the owner, and for pages with no live session (the render page).
export function JoinProvider({
  deckId,
  join,
  owner,
  children,
}: {
  deckId: string;
  join: JoinView;
  owner: boolean;
  children: ReactNode;
}) {
  return (
    <LiveSessionProvider deckId={deckId} initial={owner ? idleLiveView(join) : null}>
      <JoinValueProvider join={join}>{children}</JoinValueProvider>
    </LiveSessionProvider>
  );
}
