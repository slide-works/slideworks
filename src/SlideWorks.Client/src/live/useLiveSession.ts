import { useCallback, useState } from 'react';
import type { JoinView } from '../api/types';
import type { LiveView } from './liveView';

// The owner's live session for one deck (SW-5 J6). liveView is null for anyone who is not the deck's owner: only the
// owner may read or change the session, so the live chip is drawn only when it is set.
export interface LiveSession {
  live: boolean;
  liveView: LiveView | null;
  joinedCount: number;
  goLive(): Promise<void>;
  end(): Promise<void>;
  setJoinBar(on: boolean): Promise<void>;
}

// The session of someone who is not the owner.
export const NO_LIVE_SESSION: LiveSession = {
  live: false,
  liveView: null,
  joinedCount: 0,
  goLive: () => Promise.resolve(),
  end: () => Promise.resolve(),
  setJoinBar: () => Promise.resolve(),
};

// The not-live view the fixture starts from, for the deck's join.
export function idleLiveView(join: JoinView): LiveView {
  return {
    sessionId: null,
    code: join.code,
    codeLabel: join.codeLabel,
    joinUrl: join.joinUrl,
    hostLabel: join.hostLabel,
    live: false,
    joinedCount: 0,
    joinBar: false,
  };
}

// TODO: replace the fixture state with GET, POST and DELETE /api/decks/{id}/live, the joinedCount event and the look
// route, as the real live session. Until then going live and ending only change this hook's own state.
export function useLiveSession(
  deckId: string,
  initial: LiveView | null,
  fixtureSessionId = `fixture-${deckId}`,
): LiveSession {
  const [liveView, setLiveView] = useState<LiveView | null>(initial);
  const goLive = useCallback(async () => {
    setLiveView((view) => view && { ...view, sessionId: fixtureSessionId, live: true });
  }, [fixtureSessionId]);
  const end = useCallback(async () => {
    setLiveView((view) => view && { ...view, sessionId: null, live: false, joinedCount: 0 });
  }, []);
  const setJoinBar = useCallback(async (on: boolean) => {
    setLiveView((view) => view && { ...view, joinBar: on });
  }, []);
  if (liveView === null) return NO_LIVE_SESSION;
  return {
    live: liveView.live,
    liveView,
    joinedCount: liveView.joinedCount,
    goLive,
    end,
    setJoinBar,
  };
}
