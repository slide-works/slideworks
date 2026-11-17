import { useCallback, useState } from 'react';
import type { JoinView } from '../api/types';
import type { LiveView } from './liveView';

// The live session for one deck (SW-5 J6). liveView is null for anyone who is not the deck's owner: only the owner may
// read or change the session, so the live chip is drawn only when it is set. The caller only says the viewer may be the
// owner (initial is set); the live session confirms it, so ownership is decided in this hook alone.
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

// The view the fixture starts from, for the deck's join: live when the deck is live through the API.
export function idleLiveView(join: JoinView): LiveView {
  return {
    sessionId: join.live ? fixtureSessionId(join.code) : null,
    code: join.code,
    codeLabel: join.codeLabel,
    joinUrl: join.joinUrl,
    hostLabel: join.hostLabel,
    live: join.live,
    joinedCount: 0,
    joinBar: false,
  };
}

function fixtureSessionId(code: string): string {
  return `fixture-${code}`;
}

// _deckId is for the real session's calls. initial is the view of someone who may be the owner, or null for anyone else.
// TODO: confirm ownership from GET /api/decks/{id}/live (403 or 404 means not the owner, so no chip), and replace the
// fixture state with GET, POST and DELETE /api/decks/{id}/live, the joinedCount event and the look route, as the real
// live session. Until then going live and ending only change this hook's own state.
export function useLiveSession(_deckId: string, initial: LiveView | null): LiveSession {
  const [liveView, setLiveView] = useState<LiveView | null>(initial);
  const goLive = useCallback(async () => {
    setLiveView((view) => view && { ...view, sessionId: fixtureSessionId(view.code), live: true });
  }, []);
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
