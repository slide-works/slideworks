import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { AUDIENCE_EVENT_METHOD, isAudienceEvent } from '../../guest/audienceEvents';
import { useLiveSessionContext } from '../../live/LiveSessionProvider';
import { useDeckConnection } from '../../realtime/useDeckConnection';
import { useJoin } from '../../slides/JoinContext';
import { readQaControl } from '../presenter/qaControlApi';
import type { QaControlView } from '../types';
import { QaScreenProvider, qaScreenOf } from './QaScreenContext';

export type QaScreenLiveProps = {
  deckId: string;
  // The same flag PollScreenLive and JoinProvider take: the viewer may be a person who can present the deck
  // (DeckPlayView.isOwner). Anyone else gets null, and the provider makes no call.
  owner: boolean;
  children: ReactNode;
};

// The big screen's questions in the player and the audience window (SW-9 Q9), inside JoinProvider. For a person who can
// present the deck (the owner flag, which the play view computes from the can-present rule) and while the live session
// confirms the viewer and the deck is live, it watches the deck's audience (WatchAudience, again after every
// reconnect), reads GET /api/decks/{id}/qa on each successful watch (the first, and after a reconnect, since
// questionControl events sent while offline are lost) and follows questionControl. A view with a lower or equal seq
// than the one held is dropped. The screen is that view through qaScreenOf, so only what the room may see shows. A new
// deck, or the deck going live or not, starts from nothing. Anyone else, a page outside a JoinProvider and a deck that
// isn't live get null.
export function QaScreenLive({ deckId, owner, children }: QaScreenLiveProps) {
  const inJoin = useJoin() !== null;
  const session = useLiveSessionContext();
  const active = owner && deckId !== '' && inJoin && session.liveView !== null && session.live;
  const connection = useDeckConnection();
  const [control, setControl] = useState<QaControlView | null>(null);
  // How many times WatchAudience has succeeded on this connection.
  const [watched, setWatched] = useState(0);

  const apply = useCallback((view: QaControlView) => {
    setControl((held) => (held !== null && view.seq <= held.seq ? held : view));
  }, []);

  const scope = `${deckId}:${active}`;
  const [shownScope, setShownScope] = useState(scope);
  if (shownScope !== scope) {
    setShownScope(scope);
    setControl(null);
    setWatched(0);
  }

  useEffect(() => {
    if (!active) return;
    const stopEvents = connection.on(AUDIENCE_EVENT_METHOD, (event: unknown) => {
      if (isAudienceEvent(event) && event.type === 'questionControl') apply(event.control);
    });
    let gone = false;
    // The server forgets a dead connection's groups, so watch again each time the connection is up. A refusal means
    // this viewer can't present the deck: nothing is read and the screen stays null.
    const stopUp = connection.onUp(() => {
      connection.invoke('WatchAudience', deckId).then(
        () => {
          if (!gone) setWatched((count) => count + 1);
        },
        () => undefined,
      );
    });
    return () => {
      gone = true;
      stopEvents();
      stopUp();
    };
  }, [active, connection, deckId, apply]);

  useEffect(() => {
    if (!active || watched === 0) return;
    let gone = false;
    // A failed read leaves the screen as it was; the next reconnect or questionControl brings it up to date. The big
    // screen shows the room no error.
    readQaControl(deckId).then(
      (view) => {
        if (!gone) apply(view);
      },
      () => undefined,
    );
    return () => {
      gone = true;
    };
  }, [active, deckId, watched, apply]);

  const screen = useMemo(
    () => (active && control !== null ? qaScreenOf(control) : null),
    [active, control],
  );
  return <QaScreenProvider screen={screen}>{children}</QaScreenProvider>;
}
