import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError, api } from '../api/client';
import type { JoinView, LookChangeRequest } from '../api/types';
import { AUDIENCE_EVENT_METHOD, isAudienceEvent } from '../guest/audienceEvents';
import { useDeckConnection } from '../realtime/useDeckConnection';
import type { LiveView } from './liveView';

// The live session for one deck (SW-5 J6). liveView is null for anyone who is not the deck's owner: only the owner may
// read or change the session, so the live chip is drawn only when it is set. The caller only says the viewer may be the
// owner (initial is set); the live session confirms it, so ownership is decided in this hook alone.
export interface LiveSession {
  live: boolean;
  liveView: LiveView | null;
  joinedCount: number;
  // The message for the last failed Go live, End, join bar change or read; null when nothing failed.
  error: string | null;
  goLive(): Promise<void>;
  end(): Promise<void>;
  setJoinBar(on: boolean): Promise<void>;
}

// The session of someone who is not the owner.
export const NO_LIVE_SESSION: LiveSession = {
  live: false,
  liveView: null,
  joinedCount: 0,
  error: null,
  goLive: () => Promise.resolve(),
  end: () => Promise.resolve(),
  setJoinBar: () => Promise.resolve(),
};

// The view a page starts from, for the deck's join, until GET /live confirms the owner: live when the deck is live.
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

const liveUrl = (deckId: string) => `/api/decks/${encodeURIComponent(deckId)}/live`;

const WATCH_FAILED = 'Could not follow who has joined.';

// The live session for one deck, for the deck's owner (SW-5 J2, J4, J6). initial is set when the viewer may be the owner
// and null for anyone else. For a possible owner it reads GET /live: 200 makes them the owner (liveView is set and the
// chip is drawn), 403 or 404 means they are not. It reads again when initial.live changes, which is how a liveChanged
// event (the deck ended from another window) reaches the owner, and it follows the joined count with WatchAudience on
// the page's shared deck connection.
export function useLiveSession(deckId: string, initial: LiveView | null): LiveSession {
  const mayOwn = initial !== null && deckId !== '';
  const joinLive = initial?.live;
  const [liveView, setLiveView] = useState<LiveView | null>(null);
  const [error, setError] = useState<string | null>(null);
  const connection = useDeckConnection();
  // Latest wins: a slow read never lands over a newer answer or a Go live.
  const latest = useRef(0);

  useEffect(() => {
    if (!mayOwn) return;
    const seq = ++latest.current;
    api.get<LiveView>(liveUrl(deckId)).then(
      (view) => {
        if (seq !== latest.current) return;
        setLiveView(view);
        setError(null);
      },
      (failure: unknown) => {
        if (seq !== latest.current) return;
        if (failure instanceof ApiError && (failure.status === 403 || failure.status === 404)) {
          setLiveView(null);
        } else {
          setError('Could not read the live session.');
        }
      },
    );
    return () => {
      latest.current += 1;
    };
  }, [deckId, mayOwn, joinLive]);

  const isOwner = mayOwn && liveView !== null;
  useEffect(() => {
    if (!isOwner) return;
    const stopListening = connection.on(AUDIENCE_EVENT_METHOD, (event: unknown) => {
      if (isAudienceEvent(event) && event.type === 'joinedCount') {
        const { count } = event;
        setLiveView((view) => view && { ...view, joinedCount: count });
      }
    });
    // Each time the connection is up: the server forgets a dead connection's groups.
    const stopUp = connection.onUp(() => {
      connection.invoke('WatchAudience', deckId).catch(() => setError(WATCH_FAILED));
    });
    return () => {
      stopListening();
      stopUp();
    };
  }, [connection, deckId, isOwner]);

  // Runs one call: clears the last error, and sets failure when it fails. A newer read must not land over its result.
  const run = useCallback(async (call: () => Promise<void>, failure: string) => {
    setError(null);
    try {
      await call();
    } catch {
      setError(failure);
    }
  }, []);

  const goLive = useCallback(
    () =>
      run(async () => {
        const view = await api.post<LiveView>(liveUrl(deckId));
        latest.current += 1;
        setLiveView(view);
      }, 'Could not go live.'),
    [deckId, run],
  );
  const end = useCallback(
    () =>
      run(async () => {
        await api.del<void>(liveUrl(deckId));
        latest.current += 1;
        setLiveView((view) => view && { ...view, sessionId: null, live: false, joinedCount: 0 });
      }, 'Could not end the live session.'),
    [deckId, run],
  );
  const setJoinBar = useCallback(
    (on: boolean) =>
      run(async () => {
        const body: LookChangeRequest = { chrome: { joinBar: on } };
        await api.put(`/api/decks/${encodeURIComponent(deckId)}/look`, body);
        setLiveView((view) => view && { ...view, joinBar: on });
      }, 'Could not change the join bar.'),
    [deckId, run],
  );

  if (!mayOwn || liveView === null) return NO_LIVE_SESSION;
  return {
    live: liveView.live,
    liveView,
    joinedCount: liveView.joinedCount,
    error,
    goLive,
    end,
    setJoinBar,
  };
}
