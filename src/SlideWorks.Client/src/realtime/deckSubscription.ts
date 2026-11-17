import type { DeckEvent } from './deckEvents';
import { acquireDeckConnection, type DeckConnectionOptions } from './useDeckConnection';

export type { RealtimeConnection } from './useDeckConnection';

// The server side is src/SlideWorks.WebApi/Realtime/DeckHub.cs: JoinDeck(deckId) starts a deck's events, delivered as
// "deckEvent" messages carrying the DeckEvent JSON (deckEvents.ts).
const EVENT_METHOD = 'deckEvent';

export interface SubscribeOptions extends DeckConnectionOptions {
  // Called each time JoinDeck succeeds: the first join, and every re-join after SignalR reconnects. Events from before
  // a join are never delivered, so a screen re-reads here to catch a change it could have missed.
  onJoined?: () => void;
}

function isDeckEvent(value: unknown): value is DeckEvent {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as { type?: unknown }).type === 'string' &&
    typeof (value as { deckId?: unknown }).deckId === 'string'
  );
}

// Joins the deck on the page's shared hub connection (useDeckConnection) and calls onEvent for each of its events. A
// connection that cannot be made is retried with back-off; after SignalR reconnects a dropped connection the deck is
// joined again (the server forgets the groups of a dead connection). Events from before a join (before the first one,
// or while offline) are not replayed, so onJoined says when each join has succeeded and a screen that must not miss a
// change re-reads then. The returned function stops everything: no more events or joins, and the lease on the
// connection given back, which closes it (and cancels its retries) when nothing else on the page holds it.
export function subscribeToDeck(
  deckId: string,
  onEvent: (event: DeckEvent) => void,
  { onJoined, ...connection }: SubscribeOptions = {},
): () => void {
  const lease = acquireDeckConnection(connection);
  let stopped = false;

  const join = async () => {
    try {
      await lease.invoke('JoinDeck', deckId);
    } catch {
      // A deck the caller can't open: the screen shows its own error from the REST call, nothing to retry here.
      return;
    }
    if (!stopped) onJoined?.();
  };

  lease.on(EVENT_METHOD, (event: unknown) => {
    if (!stopped && isDeckEvent(event) && event.deckId === deckId) onEvent(event);
  });
  lease.onUp(() => {
    if (!stopped) void join();
  });

  return () => {
    stopped = true;
    lease.release();
  };
}
