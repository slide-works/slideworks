import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { AUDIENCE_EVENT_METHOD, isAudienceEvent } from '../../guest/audienceEvents';
import { useLiveSessionContext } from '../../live/LiveSessionProvider';
import { useDeckConnection } from '../../realtime/useDeckConnection';
import { useJoin } from '../../slides/JoinContext';
import { REACTION_EMOJI } from '../emoji';
import { ScreenReactionsContext, type ScreenBurst } from './useScreenReactions';

export type ScreenReactionsLiveProps = {
  deckId: string;
  // The same flag PollScreenLive and JoinProvider take: the viewer may be a person who can present the deck
  // (DeckPlayView.isOwner). Anyone else gets null, and the provider makes no call.
  owner: boolean;
  children: ReactNode;
};

// How long a burst stays in the list. The layer draws a burst the moment it renders it, so this only bounds the list.
const KEEP_MS = 5000;

// The big screen's reactions in the player and the audience window (SW-9 Q7, Q9), inside JoinProvider. For a person who
// can present the deck (the owner flag, confirmed by the deck's live session as PollScreenLive does) it watches the
// deck's audience (WatchAudience, again after every reconnect) while the deck is live and turns each reactions event
// into one burst per tap, in the order of the phone's row. Null for a viewer who can't present the deck and outside a
// JoinProvider; an empty list while not live. Ids only increase, across sessions too, so a layer can tell a burst is new.
export function ScreenReactionsLive({ deckId, owner, children }: ScreenReactionsLiveProps) {
  const inJoin = useJoin() !== null;
  const listening = owner && deckId !== '' && inJoin;
  const session = useLiveSessionContext();
  const active = listening && session.liveView !== null && session.live;
  const connection = useDeckConnection();
  const [bursts, setBursts] = useState<ScreenBurst[]>([]);
  const nextId = useRef(1);

  // A new deck or a live session ending starts from nothing.
  const scope = `${deckId}:${active}`;
  const [shownScope, setShownScope] = useState(scope);
  if (shownScope !== scope) {
    setShownScope(scope);
    setBursts([]);
  }

  useEffect(() => {
    if (!active) return;
    const timers = new Set<ReturnType<typeof setTimeout>>();
    const stopEvents = connection.on(AUDIENCE_EVENT_METHOD, (event: unknown) => {
      if (!isAudienceEvent(event) || event.type !== 'reactions') return;
      const added = REACTION_EMOJI.flatMap((emoji) =>
        Array.from({ length: Math.max(0, event.counts[emoji] ?? 0) }, () => emoji),
      ).map((emoji): ScreenBurst => ({ id: nextId.current++, emoji }));
      if (added.length === 0) return;
      setBursts((held) => [...held, ...added]);
      const ids = new Set(added.map((burst) => burst.id));
      const timer = setTimeout(() => {
        timers.delete(timer);
        setBursts((held) => held.filter((burst) => !ids.has(burst.id)));
      }, KEEP_MS);
      timers.add(timer);
    });
    // The server forgets a dead connection's groups, so watch again each time the connection is up. A refusal means this
    // viewer can't present the deck: no reactions come, and the big screen shows the room no error.
    const stopUp = connection.onUp(() => {
      connection.invoke('WatchAudience', deckId).then(undefined, () => undefined);
    });
    return () => {
      stopEvents();
      stopUp();
      timers.forEach(clearTimeout);
    };
  }, [active, connection, deckId]);

  const value = useMemo(() => (listening ? { bursts } : null), [listening, bursts]);
  return <ScreenReactionsContext value={value}>{children}</ScreenReactionsContext>;
}
