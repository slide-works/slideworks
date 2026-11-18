import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { AUDIENCE_EVENT_METHOD, isAudienceEvent } from '../../guest/audienceEvents';
import { useLiveSessionContext } from '../../live/LiveSessionProvider';
import { useDeckConnection } from '../../realtime/useDeckConnection';
import { useJoin } from '../../slides/JoinContext';
import { REACTION_EMOJI } from '../emoji';
import { ScreenReactionsContext, type ScreenBurst } from './useScreenReactions';

// The last burst id handed out. Module-wide, so it can't go backwards when the provider remounts or a new live session
// starts: a layer that has drawn id N never meets a new burst with an id of N or less.
let lastBurstId = 0;

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
// can present the deck (the owner flag, confirmed by the deck's live session as QaScreenLive does) it watches the
// deck's audience (WatchAudience, again after every reconnect) while the deck is live and turns each reactions event
// into one burst per tap, in the order of the phone's row. Null for a viewer the live session doesn't confirm and outside a
// JoinProvider; an empty list while not live. Ids only increase, across sessions too, so a layer can tell a burst is new.
export function ScreenReactionsLive({ deckId, owner, children }: ScreenReactionsLiveProps) {
  const inJoin = useJoin() !== null;
  const session = useLiveSessionContext();
  // The owner flag alone is not enough: the live session must confirm the viewer (GET /live answered), as QaScreenLive.
  const confirmed = owner && deckId !== '' && inJoin && session.liveView !== null;
  const active = confirmed && session.live;
  const connection = useDeckConnection();
  const [bursts, setBursts] = useState<ScreenBurst[]>([]);

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
      ).map((emoji): ScreenBurst => ({ id: ++lastBurstId, emoji }));
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

  const value = useMemo(() => (confirmed ? { bursts } : null), [confirmed, bursts]);
  return <ScreenReactionsContext value={value}>{children}</ScreenReactionsContext>;
}
