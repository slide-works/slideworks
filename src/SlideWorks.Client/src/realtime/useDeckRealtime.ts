import { useEffect, useRef } from 'react';
import type { DeckEvent } from './deckEvents';
import { subscribeToDeck, type SubscribeOptions } from './deckSubscription';

// Follows one deck's events for as long as the calling screen is mounted, and starts over when the deck changes.
// onJoined runs each time the deck is joined: first, and again after a reconnect. Nothing from before a join is
// delivered, so a screen re-reads there. onEvent and onJoined may change on every render without reconnecting. The
// options are for tests.
export function useDeckRealtime(
  deckId: string,
  onEvent: (event: DeckEvent) => void,
  onJoined?: () => void,
  options?: Omit<SubscribeOptions, 'onJoined'>,
): void {
  const handlers = useRef({ onEvent, onJoined });
  useEffect(() => {
    handlers.current = { onEvent, onJoined };
  }, [onEvent, onJoined]);
  useEffect(() => {
    if (deckId === '') return;
    return subscribeToDeck(deckId, (event) => handlers.current.onEvent(event), {
      ...options,
      onJoined: () => handlers.current.onJoined?.(),
    });
  }, [deckId, options]);
}
